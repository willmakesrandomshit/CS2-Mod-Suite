using System;
using System.Collections.Generic;
using System.Text;

namespace CitizenStories
{
    // Pure logic: no game types, so it can be unit-tested outside the game
    // (see scripts/CitizenStoriesTests). Every sentence is derived from a fact the
    // game reported; nothing is invented beyond choosing a name and phrasing.
    public enum AgeGroup { Child, Teen, Adult, Elderly }

    public struct CitizenFacts
    {
        public int Seed;               // stable per citizen (game's pseudo-random)
        public bool Male;
        public AgeGroup Age;
        public int Education;          // 0 uneducated .. 4 post-graduate
        public int Health;             // 0-255
        public int WellBeing;          // 0-255
        public bool IsStudent;
        public bool HasJob;
        public bool Unemployed;
        public bool Homeless;
        public bool Tourist;
        public bool Commuter;
        public int HouseholdSize;      // citizens in the household (>= 1)
        public int Rent;               // 0 if unknown/owned
        public int WorkCommuteMinutes; // 0 if unknown
        public string Doing;           // output of DescribeActivity, may be empty
        public bool Sick;
    }

    public static class StoryEngine
    {
        private static readonly string[] MaleNames = { "Arlo", "Mateo", "Jonas", "Kofi", "Leo", "Dmitri", "Hiro", "Samir", "Tobias", "Ravi", "Felix", "Oscar" };
        private static readonly string[] FemaleNames = { "Maya", "Ines", "Yuki", "Amara", "Greta", "Noor", "Elena", "Priya", "Sofia", "Tess", "Lucia", "Hana" };

        public static string NameFor(int seed, bool male)
        {
            string[] pool = male ? MaleNames : FemaleNames;
            return pool[(int)((uint)seed % (uint)pool.Length)];
        }

        public static string[] EducationLabels = { "no formal schooling", "elementary school", "high school", "a college degree", "a university degree" };

        // Pseudo-random but stable choice from the citizen's seed.
        private static T Pick<T>(int seed, int salt, IList<T> options)
        {
            uint h = ((uint)seed * 2654435761u + (uint)salt * 40503u) >> 8;
            return options[(int)(h % (uint)options.Count)];
        }

        public static string DescribeMood(int wellBeing)
        {
            if (wellBeing >= 170) return "thriving";
            if (wellBeing >= 110) return "getting by";
            if (wellBeing >= 60) return "struggling";
            return "barely holding on";
        }

        public static string DescribeActivity(string purposeName)
        {
            switch (purposeName)
            {
                case "GoingToWork": return "heading to work";
                case "Working": return "on the job";
                case "GoingHome": return "heading home";
                case "Sleeping": return "asleep";
                case "Shopping": return "out shopping";
                case "Leisure": case "Relaxing": return "out for some leisure";
                case "Studying": return "studying";
                case "GoingToSchool": return "on the way to school";
                case "Hospital": case "InHospital": return "at the hospital";
                case "Sightseeing": case "VisitAttractions": return "taking in the sights";
                case "MovingAway": return "leaving town for good";
                case "GoingToJail": case "InJail": case "InPrison": case "GoingToPrison": return "in trouble with the law";
                case "Traveling": return "on a trip";
                default: return "";
            }
        }

        public static string Headline(CitizenFacts f)
        {
            string age = f.Age == AgeGroup.Child ? "kid" : f.Age == AgeGroup.Teen ? "teenager" : f.Age == AgeGroup.Elderly ? "retiree" : (f.Male ? "man" : "woman");
            return NameFor(f.Seed, f.Male) + ", " + (f.Tourist ? "a visiting " : "a ") + age;
        }

        public static string Write(CitizenFacts f)
        {
            string name = NameFor(f.Seed, f.Male);
            string they = f.Male ? "he" : "she";
            string their = f.Male ? "his" : "her";
            var sb = new StringBuilder();

            if (f.Tourist)
                sb.Append(name).Append(" is only in town for a visit");
            else if (f.Homeless)
                sb.Append(name).Append(" has no place to call home right now");
            else
                sb.Append(name).Append(Pick(f.Seed, 1, new[] { " lives here with ", " shares a home with ", " has settled in with " })).Append(HouseholdPhrase(f.HouseholdSize));
            sb.Append(". ");

            if (f.Age == AgeGroup.Child) sb.Append("Still a child, ").Append(they).Append(" spends the days growing up here. ");
            else if (f.IsStudent) sb.Append(Cap(they)).Append(" is still studying, with ").Append(EducationLabels[Clamp(f.Education, 0, 4)]).Append(" so far. ");
            else if (f.Age == AgeGroup.Elderly) sb.Append("Retired now, ").Append(they).Append(" has ").Append(EducationLabels[Clamp(f.Education, 0, 4)]).Append(" behind ").Append(them(f.Male)).Append(". ");
            else if (f.HasJob)
            {
                sb.Append(Cap(they)).Append(" has ").Append(EducationLabels[Clamp(f.Education, 0, 4)]).Append(" and a steady job");
                if (f.WorkCommuteMinutes > 0) sb.Append(" ").Append(f.WorkCommuteMinutes).Append(" minutes from home");
                sb.Append(". ");
            }
            else if (f.Unemployed) sb.Append(Cap(they)).Append(" is out of work and looking. ");
            else sb.Append(Cap(they)).Append(" has ").Append(EducationLabels[Clamp(f.Education, 0, 4)]).Append(". ");

            if (f.Rent > 0) sb.Append("Rent takes ").Append(f.Rent).Append(" a month. ");
            if (f.Sick) sb.Append(Cap(they)).Append(" is not feeling well. ");
            sb.Append("Overall, ").Append(they).Append(" is ").Append(DescribeMood(f.WellBeing)).Append(".");
            if (!string.IsNullOrEmpty(f.Doing)) sb.Append(" Right now ").Append(they).Append(" is ").Append(f.Doing).Append(".");
            return sb.ToString();
        }

        private static string them(bool male) { return male ? "him" : "her"; }
        private static string Cap(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }
        private static string HouseholdPhrase(int size)
        {
            if (size <= 1) return "no one else";
            if (size == 2) return "one other person";
            return (size - 1) + " others";
        }
    }
}
