using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Nest;

namespace ESTests
{
    class Program
    {
        static ETTester ETTester = new ETTester();
        static void Main(string[] args)
        {
            string readLine;
            while((readLine = Console.ReadLine()) != "")
            {
                if (Commands.ContainsKey(readLine.ToLower()))
                {
                    Commands[readLine.ToLower()]();
                }
                else
                {
                    PrintHelp();
                }
                Console.WriteLine("Here we go again");
            }
            Console.WriteLine("Ending");
        }

        public static Dictionary<string, Action> Commands = new Dictionary<string, Action>()
        {
            {"h", PrintHelp},
            {"i", IndexData},
            {"ci", CreateIndex},
            {"q", Query}
        };

        private static void CreateIndex()
        {
            ETTester.CreateIndex();
        }

        private static void Query()
        {
            Console.WriteLine("Search query <empty = no search>: ");
            var searchString = Console.ReadLine();
            Console.WriteLine("Filter <empty = no filter>: ");
            var filter = Console.ReadLine();
            var result = ETTester.Search(searchString, filter);
            foreach (var aktor in result.Documents)
            {
                Console.WriteLine(aktor);
            }
            Console.WriteLine("Hightlights:");
            foreach (var highlight in result.Highlights)
            {
                Console.WriteLine(highlight.Key + ": " +
                                  string.Join(", ",
                                      highlight.Value.Select(
                                          y =>
                                              y.Key + ", " + y.Value.DocumentId + ", " + y.Value.Field + ", " +
                                              string.Join(", ", y.Value.Highlights))));
            }
        }

        private static void IndexData()
        {
            ETTester.GenerateIndexes();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Help for this simple console");
            Console.WriteLine("h: print this");
            Console.WriteLine("i: index data");
            Console.WriteLine("q: query data");
        }
    }

    public class ETTester
    {
        private ElasticClient _client;
        private Random _random;

        public ETTester()
        {
            var settings = new ConnectionSettings(new Uri("http://localhost:9200"));
            settings.SetDefaultIndex("aktorer");
            _random = new Random();
            _client = new ElasticClient(settings);
        }

        public void CreateIndex()
        {
            _client.CreateIndex("aktorer", s =>
                s.AddMapping<Aktor>(m => m.MapFromAttributes()));
        }

        public void GenerateIndexes()
        {
            var aktors = CreateAktors().ToList();
            _client.IndexMany(aktors);
//            aktors.ForEach((a) => _client.Index(a));
        }

        private IEnumerable<Aktor> CreateAktors()
        {
            var personer = CreatePersoner();
            var foretak = CreateForetak();
            return personer.Concat(foretak);
        }

        private IEnumerable<Aktor> CreateForetak()
        {
            List<string> navn = new List<string>()
            {
                "KPMG AS",
                "Gjensidige",
                "KPMG Oslo AS",
                "DNB",
                "DNB Bank",
                "Sparebank1",
                "Sparebank1 forsikring",
                "Nordea",
                "Handelsbanken",
                "Superbanken"
            };

            for (int i = 0; i < 10; i++)
            {
                yield return new Aktor()
                {
                    AktorType = AktorType.Foretak,
                    Id = "f_" + i,
                    LookupId = i,
                    Konsesjoner = CreateKonsesjoner(_random.Next(3, 7), 1).ToArray(),
                    Name = navn[i],
                    Aktiv = _random.Next(1, 10) > 7
                };
            }
        }

        private IEnumerable<Aktor> CreatePersoner()
        {
            List<string> navn = new List<string>()
            {
                "Tomas Jansson",
                "tomas awesome",
                "Mads Nyborg",
                "mads mediocre",
                "Ulf Nyborg",
                "King Jansson",
                "John Doe",
                "King Doe",
                "Kingson awesome",
                "Tomas jansson"
            };
            for (int i = 0; i < 10; i++)
            {
                yield return new Aktor()
                {
                    AktorType = AktorType.Person,
                    Id = "p_" + i,
                    LookupId = i,
                    Konsesjoner = CreateKonsesjoner(_random.Next(3,7), 6).ToArray(),
                    Name = navn[i],
                    Aktiv = _random.Next(1, 10) > 7
                };
            }
        }

        private List<Tuple<DateTime, DateTime>> ValidDates = new List<Tuple<DateTime, DateTime>>()
        {
            Tuple.Create(new DateTime(2010, 01, 01), new DateTime(2010, 12, 31)),
            Tuple.Create(new DateTime(2010, 05, 01), new DateTime(2010, 12, 31)),
            Tuple.Create(new DateTime(2010, 09, 01), new DateTime(2011, 12, 31)),
            Tuple.Create(new DateTime(2010, 02, 01), new DateTime(2010, 4, 29)),
            Tuple.Create(new DateTime(2011, 01, 01), new DateTime(2011, 05, 31))
        };

        private IEnumerable<Konsesjon> CreateKonsesjoner(int numberOfKonsesjoner, int startIdType)
        {
            for (var i = 0; i < numberOfKonsesjoner; i++)
            {
                var dateIndex = _random.Next(0, 4);
                yield return new Konsesjon
                {
                    Beskrivelse = "konsesjon av type" + i,
                    Type = _random.Next(startIdType, startIdType + 5),
                    FraDato = ValidDates[dateIndex].Item1,
                    TilDato = ValidDates[dateIndex].Item2
                };
            }
        }

        public IQueryResponse<Aktor> Search(string searchString, string filter)
        {
            var filterObject = CreateFilter(filter);
            Func<SearchDescriptor<Aktor>, SearchDescriptor<Aktor>> searchDescriptor = sd => sd.QueryString(searchString);
            if (filterObject != null)
            {
                searchDescriptor = AddFilters(searchDescriptor, filterObject);
            }
            Func<SearchDescriptor<Aktor>, SearchDescriptor<Aktor>> highlightedDescriptor = sd =>
            {
                return searchDescriptor(sd).Highlight(
                    h =>
                        h.PreTags("<b>")
                            .PostTags("</b>")
                            .OnFields(f => f.OnField(e => e.Name).PreTags("<em>").PostTags("</em>")));
            };
            var result = _client.Search(highlightedDescriptor);
            //var result =
            //    _client.Search<Aktor>(
            //        s =>
            //            s.QueryString(searchString)
            //                .Highlight(
            //                    h =>
            //                        h.PreTags("<b>")
            //                            .PostTags("</b>")
            //                            .OnFields(f => f.OnField(e => e.Name).PreTags("<em>").PostTags("</em>"))));
            return result;
        }

        private Func<SearchDescriptor<Aktor>, SearchDescriptor<Aktor>> AddFilters(Func<SearchDescriptor<Aktor>, SearchDescriptor<Aktor>> searchDescriptor, IFilterObj filterObject)
        {
            Func<SearchDescriptor<Aktor>, SearchDescriptor<Aktor>> filteredDescriptor = sd =>
            {
                return searchDescriptor(sd).Filter(f =>
                    f.Nested((n) => 
                        n.Path(p => p.Konsesjoner[0])
                        .Query(q => q.Filtered(f1 =>
                            f1.Query(qq => qq.MatchAll()).Filter(ff => ff.And(fff => fff.Term(fa => fa.Konsesjoner[0].Beskrivelse, filterObject.Value)))))));
                            
                        //.Query(dq => dq.Fil)
                        //    q.Term(f1 => f1.Konsesjoner[0].Beskrivelse, "" + filterObject.Value))));
            };
            return filteredDescriptor;
        }

        private IFilterObj CreateFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter))
                return null;
            return new FilterObj(filter);
        }
    }

    public class FilterObj : IFilterObj
    {
        public FilterObj(string value)
        {
            Value = value;
        }

        public string Value { get; set; }
    }

    public interface IFilterObj
    {
        string Value { get; set; }
    }

    public class Aktor
    {
        public string Name { get; set; }
        public string Id { get; set; }
        [ElasticProperty(Index = FieldIndexOption.not_analyzed, Type = FieldType.integer_type)]
        public AktorType AktorType { get; set; }
        [ElasticProperty(Type = FieldType.nested)]
        public Konsesjon[] Konsesjoner { get; set; }
        public int LookupId { get; set; }
        public bool Aktiv { get; set; }
        public override string ToString()
        {
            var result = string.Format("{0}, {1}, {2}, {3}, {4}", Id, Name, AktorType, LookupId, Aktiv) +
                         Environment.NewLine;
            if (Konsesjoner != null)
            {
                foreach (var konsesjon in Konsesjoner)
                {
                    result += konsesjon.ToString() + Environment.NewLine;
                }
            }
            return result;
        }
    }

    public enum AktorType
    {
        Foretak,
        Person
    }

    public class Konsesjon
    {
        [ElasticProperty(Index = FieldIndexOption.not_analyzed)]
        public string Beskrivelse { get; set; }
        [ElasticProperty(Index = FieldIndexOption.not_analyzed)]
        public int Type { get; set; }
        [ElasticProperty(Index = FieldIndexOption.no)]
        public DateTime FraDato { get; set; }
        [ElasticProperty(Index = FieldIndexOption.no)]
        public DateTime TilDato { get; set; }

        public override string ToString()
        {
            var result = string.Format("\tKonsesjon: {0}, {1}, {2}, {3}", Type, Beskrivelse, FraDato,
                TilDato);
            return result;
        }
    }
}
