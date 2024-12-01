using System.Text;

namespace Shared.Engine
{
    public static class UnicTo
    {
        static string ArrayList => "qwertyuioplkjhgfdsazxcvbnmQWERTYUIOPLKJHGFDSAZXCVBNM1234567890";
        static string ArrayListToNumber => "1234567890";

        public static string Code(int size = 8, bool IsNumberCode = false)
        {
            StringBuilder array = new StringBuilder();
            for (int i = 0; i < size; i++)
            {
                array.Append(ArrayList[Random.Shared.Next(0, 61)]);
            }

            return array.ToString();
        }

        public static string Number(int size = 8)
        {
            StringBuilder array = new StringBuilder();
            for (int i = 0; i < size; i++)
            {
                array.Append(ArrayListToNumber[Random.Shared.Next(0, 9)]);
            }

            return array.ToString();
        }
    }
}
