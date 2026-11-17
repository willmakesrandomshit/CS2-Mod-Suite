using CitizenStories;

int passed = 0;
void Check(string name, bool ok) { if (!ok) { Console.WriteLine("FAIL: " + name); Environment.Exit(1); } passed++; }

var worker = new CitizenFacts { Seed = 7, Male = false, Age = AgeGroup.Adult, Education = 3, WellBeing = 200, HasJob = true, HouseholdSize = 3, Rent = 450, WorkCommuteMinutes = 22, Doing = "heading to work" };
string s = StoryEngine.Write(worker);
Check("deterministic", s == StoryEngine.Write(worker));
Check("mentions commute", s.Contains("22 minutes"));
Check("mentions rent", s.Contains("450"));
Check("household of 3 -> 2 others", s.Contains("2 others"));
Check("female pronoun", s.Contains(" she ") || s.Contains("She "));
Check("activity appended", s.EndsWith("heading to work."));
Check("education label", s.Contains("a college degree"));

var homeless = new CitizenFacts { Seed = 1, Male = true, Age = AgeGroup.Adult, Homeless = true, Unemployed = true, WellBeing = 20, HouseholdSize = 1 };
string h = StoryEngine.Write(homeless);
Check("homeless", h.Contains("no place to call home") && h.Contains("barely holding on") && h.Contains("out of work"));

var kid = new CitizenFacts { Seed = 5, Male = true, Age = AgeGroup.Child, WellBeing = 120, HouseholdSize = 4 };
Check("child text", StoryEngine.Write(kid).Contains("Still a child"));
Check("student", StoryEngine.Write(new CitizenFacts { Seed = 2, Age = AgeGroup.Teen, IsStudent = true, Education = 1, HouseholdSize = 1 }).Contains("still studying"));
Check("out-of-range education is clamped", StoryEngine.Write(new CitizenFacts { Seed = 2, Age = AgeGroup.Adult, Education = 99, HouseholdSize = 1 }).Length > 10);
Check("negative seed ok", StoryEngine.NameFor(-5, true).Length > 0 && StoryEngine.NameFor(int.MinValue, false).Length > 0);
Check("headline", StoryEngine.Headline(kid).Contains("kid") && StoryEngine.Headline(new CitizenFacts { Seed = 3, Tourist = true, Age = AgeGroup.Adult }).Contains("visiting"));
Check("activity map", StoryEngine.DescribeActivity("Sleeping") == "asleep" && StoryEngine.DescribeActivity("Nonsense") == "");
Check("no unfilled braces or double spaces", !s.Contains("  ") && !s.Contains("{") );
Console.WriteLine($"Citizen Stories: {passed} offline assertions passed.");
