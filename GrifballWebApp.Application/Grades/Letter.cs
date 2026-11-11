namespace GrifballWebApp.Application.Grades;

public class Letter
{
    public Letter(string name, double value, double seed)
    {
        Name = name;
        Value = value;
        Seed = seed;
    }

    public string Name { get; set; }
    public double Value { get; set; }
    public double Seed { get; set; }
}

public class LetterPercentile
{
    public required Letter Letter { get; set; }
    public double Value { get; set; }
}