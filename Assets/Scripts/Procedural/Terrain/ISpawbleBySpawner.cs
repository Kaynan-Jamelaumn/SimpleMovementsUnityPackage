using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public interface ISpawbleBySpawner
{
    int MaxInstances { get; set; }
    int CurrentInstances { get; set; }
}
