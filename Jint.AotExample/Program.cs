using Jint;

var engine = new Engine(cfg => cfg
    .AllowClr()
);

engine.SetValue("company", new Company());

Console.WriteLine($"Company's name: {engine.Evaluate("company.name")}");
Console.WriteLine($"Company's field: {engine.Evaluate("company.field")}");
Console.WriteLine($"Company's indexer: {engine.Evaluate("company[42]")}");
Console.WriteLine($"Company's greeting: {engine.Evaluate("company.sayHello('Mary')")}");


public class Company
{
    public string Field = "public field value";
    public string Name => "Jint";
    public string SayHello(string name) => $"Hello {name}!";
    public int this[int index] => index;
}

