namespace TestApp;

class Program
{
    static void Launch(int extent, Action<int> action)
    {
        for (int i = 0; i < extent; i++)
            action(i);
    }

    public static void InvokeA(Action<int> action, int c)
    {
        InvokeB(i =>
        {
            Console.WriteLine(c);
            action(i + c);
        });
    }

    public static void InvokeB(Action<int> action)
    {
        Launch(2, action);
    }

    public static void Main(string[] args)
    {
        int t = 3;
        int c = 4;

        InvokeA(i => Console.WriteLine(i + t), c);
    }
}
