namespace Merino
{
    public static class MerinoUtils
    {
        public static string CleanYarnField(string inputString, bool extraClean = false)
        {
            if (extraClean)
            {
                return inputString.Replace("===", " ")
                    .Replace("---", " ")
                    .Replace("title:", " ")
                    .Replace("tags:", " ")
                    .Replace("position:", " ")
                    .Replace("colorID:", " ");
            }
			
            return inputString.Replace("===", " ")
                .Replace("---", " ");
        }
    }
}
