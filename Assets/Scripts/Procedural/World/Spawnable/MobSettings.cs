﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


[System.Serializable]
public class MobSettings : BaseSettings
{
    public List<SpawnableMob> prefabs;
    public int maxNumberOfMobs;

}

