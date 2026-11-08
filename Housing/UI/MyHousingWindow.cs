using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The player's own housing, from anywhere (no NPC; open it with <see cref="HousingWindows.OpenMine(CombatEntity)"/>):
/// the <b>My home</b> and <b>Account</b> pages of the realtor's window, from the view the housing office sends a player
/// who stands at no desk. Renewals and protection, letting others in or out, withdrawing an order and clearing messages
/// work here; deposits, withdrawals and claims need a realtor or an innkeeper (the pages say so).
/// <para>Built from code, one per machine. While shown it is an exclusive panel of the player's inventory (the cursor is
/// free, gameplay keys wait, the gameplay HUD steps aside); Escape closes it through the pause menu, or by itself when
/// the player has no inventory.</para>
/// </summary>
[DisallowMultipleComponent]
public class MyHousingWindow : MonoBehaviour, IExclusivePanel, IHousingHost
{
    private const int Order = 58;
    private static MyHousingWindow instance;

    private enum Tab { Home, Account }

    private CombatEntity player;
    private InventoryManager registeredWith;
    private bool managesCursor, hudFocus;
    private CursorLockMode previousLock;
    private bool previousVisible;

    private Canvas canvas;
    private RectTransform window;
    private TextMeshProUGUI title, subtitle, purse;
    private Button homeTab, accountTab;
    private HousingWindowCore core;
    private HousingHomePage home;
    private HousingAccountPage account;
    private RealtorView view;
    private InnView innView;
    /// <summary>Why the housing office cannot answer (empty: it can); there is no view meanwhile.</summary>
    private string closed = "";
    private bool viewDirty;
    private Tab tab = Tab.Home;
    private float nextFit;

    public string PanelName => "My housing";

    /// <summary>The window is shown.</summary>
    public static bool IsOpen => instance != null && instance.player != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

    CombatEntity IHousingHost.Player => player;
    bool IHousingHost.AtDesk => false;
    void IHousingHost.Send(HousingRequest request) => core.Send(request);
    void IHousingHost.Ask(string text, string yes, Action confirmed, bool danger, Action cancelled) => core.Ask(text, yes, confirmed, danger, cancelled);
    void IHousingHost.Say(string text, bool ok) => core.Say(text, ok);

    // ------------------------------------------------------------------ opening
    /// <summary>Shows the housing of <paramref name="p"/> (the local player).</summary>
    public static void Show(CombatEntity p)
    {
        if (p == null) return;
        if (instance == null)
        {
            InventoryEventSystems.EnsureSingle();
            var go = new GameObject("My Housing Window");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MyHousingWindow>();
            instance.Build();
        }
        instance.Open(p);
    }

    /// <summary>Closes the window.</summary>
    public static void Hide()
    {
        if (instance != null) instance.Close();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    private void Open(CombatEntity p)
    {
        if (player != p)
        {
            Detach();
            home.Reset();
            account.Reset();
            view = null;
            innView = null;
            closed = "";
        }
        player = p;
        HousingClient.RealtorViewArrived -= OnView;
        HousingClient.RealtorViewArrived += OnView;
        HousingClient.InnViewArrived -= OnInnView;
        HousingClient.InnViewArrived += OnInnView;
        canvas.gameObject.SetActive(true);
        Register();
        HousingUI.FitToCanvas(window, HousingUI.WindowSize);
        SetTab(tab);
        core.Begin(p); // with no NPC the office answers with the player's own housing
    }

    private void Close()
    {
        Detach();
        SetHudFocus(false);
        Unregister();
        if (canvas != null && canvas.gameObject.activeSelf) canvas.gameObject.SetActive(false);
    }

    private void Detach()
    {
        HousingClient.RealtorViewArrived -= OnView;
        HousingClient.InnViewArrived -= OnInnView;
        if (core != null) core.End();
        player = null;
        viewDirty = false;
    }

    /// <summary>Escape or the pause menu: a question closes first, then the window.</summary>
    public void ClosePanel()
    {
        if (core != null && core.Asking)
        {
            core.CloseConfirm();
            return;
        }
        Close();
    }

    private void OnDestroy()
    {
        Close();
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (ReferenceEquals(player, null)) return;
        if (player == null || !player.isActiveAndEnabled)
        {
            Close();
            return;
        }
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        SetHudFocus(true);
        if (!Typing() && CancelPressed() && !PauseMenuClosesUs())
        {
            ClosePanel();
            return;
        }
        core.Tick();
        if (viewDirty && !HousingUI.PointerHeld())
        {
            viewDirty = false;
            Render();
        }
        if (Time.unscaledTime >= nextFit)
        {
            nextFit = Time.unscaledTime + 1f;
            HousingUI.FitToCanvas(window, HousingUI.WindowSize);
        }
    }

    private void SetHudFocus(bool on)
    {
        if (on == hudFocus) return;
        hudFocus = on;
        if (on) HudFocus.Take();
        else HudFocus.Release();
    }

    private void Register()
    {
        InputBindingStore.BlockGameplayInput(this);
        if (registeredWith != null || managesCursor) return;
        InventoryManager inv = player != null ? InventoryManager.For(player) : null;
        if (inv != null)
        {
            registeredWith = inv;
            inv.RegisterPanel(this);
        }
        else
        {
            managesCursor = true;
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
        }
    }

    private void Unregister()
    {
        InputBindingStore.UnblockGameplayInput(this);
        if (registeredWith != null)
        {
            InventoryManager inv = registeredWith;
            registeredWith = null;
            inv.UnregisterPanel(this);
        }
        if (managesCursor)
        {
            managesCursor = false;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }

    // ------------------------------------------------------------------ keys
    /// <summary>The pause menu closes registered panels with Escape itself (so the window must not also react).</summary>
    private bool PauseMenuClosesUs()
    {
        PauseMenuManager pm = PauseMenuManager.Instance;
        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        return escape && registeredWith != null && pm != null && pm.isActiveAndEnabled && pm.closeInventoryFirst;
    }

    private static bool CancelPressed()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) return true;
        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }

    private bool Typing()
    {
        EventSystem es = EventSystem.current;
        GameObject selected = es != null ? es.currentSelectedGameObject : null;
        if (selected == null || canvas == null || !selected.transform.IsChildOf(canvas.transform)) return false;
        TMP_InputField field = selected.GetComponent<TMP_InputField>();
        return field != null && field.isFocused;
    }

    // ------------------------------------------------------------------ building
    private void Build()
    {
        canvas = UITheme.Canvas("My Housing", Order, transform);
        Transform c = canvas.transform;
        Image backdrop = UITheme.Panel("Backdrop", c, new Color(0f, 0f, 0f, 0.35f), 0);
        InventoryUIFactory.Stretch(backdrop.rectTransform);
        Image win = UITheme.Window("Window", c, HousingUI.WindowSize);
        window = win.rectTransform;
        Transform w = win.transform;

        HousingUI.Header(w, Close, out title, out subtitle, out purse);
        HousingUI.Set(title, "My housing");
        RectTransform tabs = UITheme.Area("Tabs", w, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -142f), new Vector2(-24f, -98f));
        InventoryUIFactory.Horizontal(tabs.gameObject, 8f);
        homeTab = UITheme.Button("Home Tab", tabs, "My home", () => SetTab(Tab.Home), UITheme.ButtonStyle.Tab, 190f, 42f);
        accountTab = UITheme.Button("Account Tab", tabs, "Account", () => SetTab(Tab.Account), UITheme.ButtonStyle.Tab, 190f, 42f);

        RectTransform body = UITheme.Area("Body", w, Vector2.zero, Vector2.one, new Vector2(24f, 58f), new Vector2(-24f, -150f));
        home = new HousingHomePage(this, UITheme.Area("My Home", body, Vector2.zero, Vector2.one));
        account = new HousingAccountPage(this, UITheme.Area("Account", body, Vector2.zero, Vector2.one), true);

        TextMeshProUGUI status = HousingUI.StatusLine(w);
        HousingConfirm confirm = HousingUI.Confirm(w);
        core = new HousingWindowCore(status, confirm)
        {
            Own = true, // the player's own housing, wherever they stand
            HasView = () => view != null || innView != null,
            Answered = (kind, reply) =>
            {
                home.Answered(kind, reply);
                account.Answered(kind, reply);
            },
        };
        canvas.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ showing
    private void OnView(RealtorView v)
    {
        if (player == null || v == null || !v.own) return; // a realtor's window's view is not this one's
        core.Viewed();
        viewDirty = true;
        innView = null;
        if (!string.IsNullOrEmpty(v.closed))
        {
            // The housing office cannot answer: nothing in the view is real.
            Closed(v.closed);
            return;
        }
        closed = "";
        view = HousingUI.Fix(v);
    }

    // Talking to an innkeeper at the same time, the office sends the inn's view: its account is shown.
    private void OnInnView(InnView v)
    {
        if (player == null || v == null || view != null) return;
        viewDirty = true;
        if (!string.IsNullOrEmpty(v.closed))
        {
            Closed(v.closed);
            return;
        }
        closed = "";
        innView = HousingUI.Fix(v);
    }

    private void Closed(string why)
    {
        closed = HousingUI.Clean(why).Trim();
        if (closed.Length == 0) closed = "The housing office is closed.";
        view = null;
        innView = null;
    }

    private void SetTab(Tab t)
    {
        tab = t;
        home.Root.SetActive(t == Tab.Home);
        account.Root.SetActive(t == Tab.Account);
        UITheme.SetSelected(homeTab, t == Tab.Home);
        UITheme.SetSelected(accountTab, t == Tab.Account);
        Render();
    }

    private void Render()
    {
        try
        {
            AccountView a = view != null ? view.account : innView != null ? innView.account : null;
            HousingUI.Set(purse, a != null ? "Purse: " + HousingUI.Money(a.wallet, a.currency) : "");
            HousingUI.Set(subtitle, a == null ? (closed.Length > 0 ? closed : "Asking the housing office...") : "Deposits, withdrawals and claims are made at a realtor or an innkeeper.");
            int leases = view != null ? view.leases.Count : 0;
            HousingUI.SetLabel(homeTab, leases > 0 ? $"My home ({leases})" : "My home");
            int waiting = a != null ? a.notices.Count + a.held.Count : 0;
            HousingUI.SetLabel(accountTab, waiting > 0 ? $"Account ({waiting})" : "Account");
            double spd = view != null ? view.secondsPerDay : innView != null ? innView.secondsPerDay : 86400.0;
            bool game = view != null ? view.gameTime : innView != null && innView.gameTime;
            if (tab == Tab.Home)
                home.Show(view, closed.Length > 0 ? closed
                    : view == null && innView != null ? "You are talking to an innkeeper: close that window to see your houses here." : null);
            else
                account.Show(a, view, spd, game, closed.Length > 0 ? closed : null);
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
        }
    }
}
