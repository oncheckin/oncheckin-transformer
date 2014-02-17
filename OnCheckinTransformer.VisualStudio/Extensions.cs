namespace OnCheckinTransformer.VisualStudio
{
    public static class Extensions
    {
        public static string ToCamelCase(this string input)
        {
            if (input.Length < 2) return input;
            return string.Format("{0}{1}", input.Substring(0,1).ToUpper(), input.Substring(1,input.Length-1));
        }
    }
}
