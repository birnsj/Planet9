using System;

namespace Planet9
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new Planet9Game())
                game.Run();
        }
    }
}






