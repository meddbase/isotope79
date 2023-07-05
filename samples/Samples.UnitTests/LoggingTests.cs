using System;
using LanguageExt;
using LanguageExt.Common;
using OpenQA.Selenium.Chrome;
using Xunit;
using Xunit.Abstractions;
using Isotope80;
using static LanguageExt.Prelude;
using static Isotope80.Isotope;

namespace Samples.UnitTests
{
    public class LoggingTests
    {
        protected readonly ITestOutputHelper Output;

        public LoggingTests(ITestOutputHelper output)
        {
            Output = output;
        }
        
        [Fact]
        public void TestNestedContextualLogs()
        {
            Seq<string> expected = Seq(
                "INFO: Info log",
                "Test 1",
                "    INFO: Info for test 1",
                "    INFO: More info for test 1",
                "    Test 1.SubTest 1",
                "        INFO: Info for test Test 1.SubTest 1",
                "        INFO: More info for test Test 1.SubTest 1",
                "    Test 1.SubTest 2",
                "        WARN: Info for test Test 1.SubTest 2",
                "        WARN: More info for test Test 1.SubTest 2");
            
            Seq<string> logs = default;
            
            var stgs = IsotopeSettings.Create();
            stgs.LogStream.Subscribe(x => logs = logs.Add(x.ToString()));
            stgs.LogStream.Subscribe(x => Output.WriteLine(x.ToVerboseString()));

            var iso2 = from _ in info("Info log")
                       from r in context("Test 1",
                                         from a in info("Info for test 1")
                                         from b in info("More info for test 1")
                                         from c in context("Test 1.SubTest 1",
                                                           from d in info("Info for test Test 1.SubTest 1")
                                                           from e in info("More info for test Test 1.SubTest 1")
                                                           select unit)
                                         from f in context("Test 1.SubTest 2",
                                                           from g in warn("Info for test Test 1.SubTest 2")
                                                           from h in warn("More info for test Test 1.SubTest 2")
                                                           select unit)
                                         select unit)
                       select r;
                                              
                                
            (var state, var value) = iso2.Run(stgs);
            
            Assert.True(logs == expected);
            Assert.True(state.Log.ToSeq() == expected);
        }
        
        [Fact]
        public void TestNestedContextualErrors()
        {
            var stgs = IsotopeSettings.Create();

            var iso = context("Chrome",
                           context("Start Page",
                                context("Patient tile",
                                     fail<Unit>("element not found"))));
                                
            (var state, var value) = iso.Run(stgs);

            Assert.True(state.Error.Head.Message == "element not found (Chrome → Start Page → Patient tile)");
        }
        
        [Fact]
        public void TestPipelineError()
        {
            var errMsg = "This is error messsage";
            var actual = "";
            var stgs   = IsotopeSettings.Create();

            var iso = fail<Unit>(errMsg) |
                      (errs => from _1 in pure(unit)
                               let _2 = actual = errs.Head.Message
                               select unit);
                                
            (var state, var value) = iso.Run(stgs);

            Assert.Equal(errMsg, actual);
        }

        [Fact]
        public void TestWaitUntilError()
        {
            var index    = 0;
            var expected = Seq("Fail 1", "Fail 4", "Timed out");

            var testIso = from _1 in pure(unit)
                          let _2 = ++index
                          from _3 in fail<Unit>($"Fail {index}")
                          select unit;

            var stgs = IsotopeSettings.Create();

            var iso = waitUntil(testIso, _ => true,
                                interval: TimeSpan.FromMilliseconds(300),
                                wait: TimeSpan.FromMilliseconds(1050));

            (var state, var value) = iso.Run(stgs);

            Assert.Equal(expected.ToString(), state.Error.ToString());
        }
    }
}