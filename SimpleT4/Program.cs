using System;

namespace SimpleT4
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: SimpleT4.exe <InputTt> <OutputCs>");
                return;
            }

            string ttFile = args[0];
            string csFile = args[1];

            Console.WriteLine("Transforming template '{0}' to intermediate assembly '{1}'...", ttFile, csFile);

            Engine engine = new Engine(ttFile, csFile);
            engine.Transform();

            Console.WriteLine("Transformation completed.");
        }
    }
}
