//using System.Collections.Generic;
//using System.Linq;
//using UnityEngine;

///// <summary>
///// Utility class providing additional features for character creation
///// Attach this to the same GameObject as CharacterCreationUI for extended functionality
///// </summary>
//public class CharacterCreationUtilities : MonoBehaviour
//{
//    [Header("Name Generation")]
//    [SerializeField] private List<string> maleNames = new List<string>();
//    [SerializeField] private List<string> femaleNames = new List<string>();
//    [SerializeField] private List<string> unisexNames = new List<string>();
//    [SerializeField] private List<string> surnames = new List<string>();
//    [SerializeField] private bool includeRandomSurnames = true;

//    [Header("Name Validation")]
//    [SerializeField] private List<string> bannedWords = new List<string>();
//    [SerializeField] private int minNameLength = 2;
//    [SerializeField] private int maxNameLength = 20;
//    [SerializeField] private bool allowNumbers = false;
//    [SerializeField] private bool allowSpecialCharacters = false;

//    [Header("Character Presets")]
//    [SerializeField] private List<CharacterPreset> characterPresets = new List<CharacterPreset>();

//    [Header("Trait Recommendations")]
//    [SerializeField] private bool enableTraitRecommendations = true;
//    [SerializeField] private int maxRecommendations = 3;

//    private CharacterCreationUI characterCreationUI;

//    [System.Serializable]
//    public class CharacterPreset
//    {
//        public string presetName;
//        public PlayerClass playerClass;
//        public List<Trait> recommendedTraits = new List<Trait>();
//        public string description;
//        [TextArea(2, 3)]
//        public string backstory;
//    }

//    public enum NameGender
//    {
//        Male,
//        Female,
//        Unisex,
//        Random
//    }

//    private void Awake()
//    {
//        characterCreationUI = GetComponent<CharacterCreationUI>();
//        InitializeDefaultNames();
//    }

//    private void InitializeDefaultNames()
//    {
//        // Add default names if lists are empty
//        if (maleNames.Count == 0)
//        {
//            maleNames.AddRange(new string[]
//            {
//                "Aiden", "Blake", "Cole", "Derek", "Ethan", "Finn", "Garrett", "Hunter",
//                "Ivan", "Jack", "Kane", "Liam", "Marcus", "Noah", "Owen", "Parker",
//                "Quinn", "Ryan", "Sean", "Tyler", "Victor", "Wade", "Xavier", "Zach"
//            });
//        }

//        if (femaleNames.Count == 0)
//        {
//            femaleNames.AddRange(new string[]
//            {
//                "Aria", "Belle", "Claire", "Diana", "Emma", "Faith", "Grace", "Hope",
//                "Iris", "Jane", "Kate", "Luna", "Maya", "Nova", "Olivia", "Paige",
//                "Quinn", "Rose", "Sara", "Tessa", "Uma", "Vera", "Willow", "Zara"
//            });
//        }

//        if (unisexNames.Count == 0)
//        {
//            unisexNames.AddRange(new string[]
//            {
//                "Alex", "Blake", "Casey", "Drew", "Emery", "Finley", "Gray", "Harper",
//                "Jordan", "Kai", "Logan", "Morgan", "Parker", "Quinn", "Riley", "Sage",
//                "Taylor", "Val"
//            });
//        }

//        if (surnames.Count == 0)
//        {
//            surnames.AddRange(new string[]
//            {
//                "Ashford", "Blackwood", "Crawford", "Donovan", "Everett", "Fletcher",
//                "Garrison", "Hartwell", "Ironwood", "Kingsley", "Lancaster", "Morrison",
//                "Northcott", "Pemberton", "Ravencroft", "Steelwright", "Thornfield",
//                "Underwood", "Westbrook", "Youngblood"
//            });
//        }

//        if (bannedWords.Count == 0)
//        {
//            bannedWords.AddRange(new string[]
//            {
//                "admin", "test", "null", "undefined", "delete", "drop", "select"
//            });
//        }
//    }


//    public string GenerateRandomName(NameGender gender = NameGender.Random)
//    {
//        List<string> namePool = GetNamePoolForGender(gender);

//        if (namePool.Count == 0)
//        {
//            return "Adventurer";
//        }

//        string firstName = namePool[Random.Range(0, namePool.Count)];

//        if (includeRandomSurnames && surnames.Count > 0)
//        {
//            string surname = surnames[Random.Range(0, surnames.Count)];
//            return $"{firstName} {surname}";
//        }

//        return firstName;
//    }

//    private List<string> GetNamePoolForGender(NameGender gender)
//    {
//        return gender switch
//        {
//            NameGender.Male => maleNames,
//            NameGender.Female => femaleNames,
//            NameGender.Unisex => unisexNames,
//            NameGender.Random => GetRandomGenderNamePool(),
//            _ => unisexNames
//        };
//    }

//    private List<string> GetRandomGenderNamePool()
//    {
//        var allNames = new List<string>();
//        allNames.AddRange(maleNames);
//        allNames.AddRange(femaleNames);
//        allNames.AddRange(unisexNames);
//        return allNames;
//    }

//    public List<string> GenerateNameSuggestions(int count = 5, NameGender gender = NameGender.Random)
//    {
//        var suggestions = new List<string>();
//        var namePool = GetNamePoolForGender(gender);

//        // Ensure we don't request more names than available
//        count = Mathf.Min(count, namePool.Count);

//        var usedNames = new HashSet<string>();

//        for (int i = 0; i < count; i++)
//        {
//            string name;
//            int attempts = 0;
//            do
//            {
//                name = GenerateRandomName(gender);
//                attempts++;
//            } while (usedNames.Contains(name) && attempts < 50);

//            if (!usedNames.Contains(name))
//            {
//                suggestions.Add(name);
//                usedNames.Add(name);
//            }
//        }

//        return suggestions;
//    }

//    public bool IsValidName(string name, out string errorMessage)
//    {
//        errorMessage = "";

//        if (string.IsNullOrWhiteSpace(name))
//        {
//            errorMessage = "Name cannot be empty";
//            return false;
//        }

//        name = name.Trim();

//        if (name.Length < minNameLength)
//        {
//            errorMessage = $"Name must be at least {minNameLength} characters";
//            return false;
//        }

//        if (name.Length > maxNameLength)
//        {
//            errorMessage = $"Name cannot exceed {maxNameLength} characters";
//            return false;
//        }

//        if (!allowNumbers && name.Any(char.IsDigit))
//        {
//            errorMessage = "Name cannot contain numbers";
//            return false;
//        }

//        if (!allowSpecialCharacters && name.Any(c => !char.IsLetterOrDigit(c) && c != ' ' && c != '\'' && c != '-'))
//        {
//            errorMessage = "Name contains invalid characters";
//            return false;
//        }

//        if (ContainsBannedWords(name))
//        {
//            errorMessage = "Name contains inappropriate content";
//            return false;
//        }

//        return true;
//    }

//    private bool ContainsBannedWords(string name)
//    {
//        string lowerName = name.ToLower();
//        return bannedWords.Any(banned => lowerName.Contains(banned.ToLower()));
//    }

//    public string SanitizeName(string name)
//    {
//        if (string.IsNullOrWhiteSpace(name))
//            return "";

//        name = name.Trim();

//        // Remove numbers if not allowed
//        if (!allowNumbers)
//        {
//            name = new string(name.Where(c => !char.IsDigit(c)).ToArray());
//        }

//        // Remove special characters if not allowed
//        if (!allowSpecialCharacters)
//        {
//            name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-').ToArray());
//        }

//        // Limit length
//        if (name.Length > maxNameLength)
//        {
//            name = name.Substring(0, maxNameLength);
//        }

//        return name;
//    }


//    public List<CharacterPreset> GetAvailablePresets()
//    {
//        return new List<CharacterPreset>(characterPresets);
//    }

//    public CharacterPreset GetPresetByName(string presetName)
//    {
//        return characterPresets.FirstOrDefault(p => p.presetName.Equals(presetName, System.StringComparison.OrdinalIgnoreCase));
//    }

//    public void ApplyPreset(CharacterPreset preset)
//    {
//        if (preset == null || characterCreationUI == null) return;

//        // This would require additional methods in CharacterCreationUI to set values programmatically
//        Debug.Log($"Applying preset: {preset.presetName}");

//        // You would implement this based on your CharacterCreationUI structure
//        // characterCreationUI.SetSelectedClass(preset.playerClass);
//        // characterCreationUI.SetRecommendedTraits(preset.recommendedTraits);
//    }


//    public List<Trait> GetRecommendedTraitsForClass(PlayerClass playerClass, TraitDatabase traitDatabase)
//    {
//        if (playerClass == null || traitDatabase == null || !enableTraitRecommendations)
//            return new List<Trait>();

//        var recommendations = new List<Trait>();

//        // Get traits that synergize with class strengths
//        var preferredTypes = playerClass.GetStrengths();
//        foreach (var traitType in preferredTypes)
//        {
//            var traitsOfType = traitDatabase.GetTraitsByType(traitType)
//                .Where(t => t.IsAvailableForClass(playerClass) && t.IsPositive)
//                .OrderBy(t => playerClass.GetTraitCost(t))
//                .Take(2);

//            recommendations.AddRange(traitsOfType);
//        }

//        // Add some generally useful traits
//        var universalTraits = traitDatabase.GetAllTraits()
//            .Where(t => t.IsAvailableForClass(playerClass) &&
//                       t.cost <= 2 &&
//                       t.IsPositive &&
//                       !recommendations.Contains(t))
//            .OrderBy(t => t.cost)
//            .Take(maxRecommendations - recommendations.Count);

//        recommendations.AddRange(universalTraits);

//        return recommendations.Take(maxRecommendations).ToList();
//    }

//    public string GetTraitRecommendationReason(Trait trait, PlayerClass playerClass)
//    {
//        if (trait == null || playerClass == null) return "";

//        var strengths = playerClass.GetStrengths();

//        if (strengths.Contains(trait.type))
//        {
//            return $"Synergizes with {playerClass.GetClassName()} strengths";
//        }

//        if (trait.cost <= 1)
//        {
//            return "Low cost, high value";
//        }

//        if (trait.effects.Any(e => e.targetStat.ToLower() == "health"))
//        {
//            return "Improves survivability";
//        }

//        return "Generally useful trait";
//    }


//    public void AddRandomNameToInputField()
//    {
//        if (characterCreationUI != null)
//        {
//            string randomName = GenerateRandomName();
//            // You would need to add a public method to CharacterCreationUI to set the name
//            // characterCreationUI.SetCharacterName(randomName);
//            Debug.Log($"Generated random name: {randomName}");
//        }
//    }

//    public Dictionary<string, object> GetCharacterCreationStats()
//    {
//        return new Dictionary<string, object>
//        {
//            ["TotalNames"] = maleNames.Count + femaleNames.Count + unisexNames.Count,
//            ["TotalPresets"] = characterPresets.Count,
//            ["RecommendationsEnabled"] = enableTraitRecommendations,
//            ["NameValidationEnabled"] = bannedWords.Count > 0
//        };
//    }

//    public void ExportCharacterData(string characterName, PlayerClass selectedClass, List<Trait> selectedTraits)
//    {
//        var characterData = new
//        {
//            Name = characterName,
//            Class = selectedClass?.GetClassName(),
//            Traits = selectedTraits?.Select(t => t.Name).ToArray(),
//            CreationTime = System.DateTime.Now,
//            Stats = selectedClass != null ? CalculateFinalStats(selectedClass, selectedTraits) : null
//        };

//        string json = JsonUtility.ToJson(characterData, true);
//        Debug.Log($"Character Data Export:\n{json}");

//        // You could save this to a file or send to a server
//        SaveCharacterToFile(characterName, json);
//    }

//    private void SaveCharacterToFile(string characterName, string jsonData)
//    {
//        string fileName = $"character_{characterName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
//        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);

//        try
//        {
//            System.IO.File.WriteAllText(filePath, jsonData);
//            Debug.Log($"Character saved to: {filePath}");
//        }
//        catch (System.Exception e)
//        {
//            Debug.LogError($"Failed to save character: {e.Message}");
//        }
//    }

//    private Dictionary<string, float> CalculateFinalStats(PlayerClass playerClass, List<Trait> traits)
//    {
//        var stats = new Dictionary<string, float>
//        {
//            ["Health"] = playerClass.health,
//            ["Stamina"] = playerClass.stamina,
//            ["Mana"] = playerClass.mana,
//            ["Speed"] = playerClass.speed,
//            ["Strength"] = playerClass.strength,
//            ["Agility"] = playerClass.agility,
//            ["Intelligence"] = playerClass.intelligence,
//            ["Endurance"] = playerClass.endurance
//        };

//        if (traits != null)
//        {
//            foreach (var trait in traits)
//            {
//                foreach (var effect in trait.effects)
//                {
//                    if (stats.ContainsKey(effect.targetStat))
//                    {
//                        switch (effect.effectType)
//                        {
//                            case TraitEffectType.StatAddition:
//                                stats[effect.targetStat] += effect.value;
//                                break;
//                            case TraitEffectType.StatMultiplier:
//                                stats[effect.targetStat] *= effect.value;
//                                break;
//                        }
//                    }
//                }
//            }
//        }

//        return stats;
//    }


//    [ContextMenu("Generate Random Names")]
//    private void DebugGenerateRandomNames()
//    {
//        var names = GenerateNameSuggestions(10);
//        Debug.Log($"Random Names: {string.Join(", ", names)}");
//    }

//    [ContextMenu("Test Name Validation")]
//    private void DebugTestNameValidation()
//    {
//        string[] testNames = { "John", "A", "VeryLongNameThatExceedsLimit", "Test123", "Normal Name" };

//        foreach (string name in testNames)
//        {
//            bool valid = IsValidName(name, out string error);
//            Debug.Log($"Name: '{name}' - Valid: {valid} - Error: {error}");
//        }
//    }


//}