using System;

namespace TidyMemo.Models;

public class RenamerDateType
{
    public RenamerDateType(string name, DateType dateType)
    {
        Name = name;
        DateType = dateType;
    }
    public string Name { get;  }
    public DateType DateType { get; }
}