// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis.EditAndContinue;
using Microsoft.CodeAnalysis.EditAndContinue.UnitTests;
using Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.EditAndContinue;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.EditAndContinue.UnitTests
{
    public class RudeEditStatementTests : RudeEditTestBase
    {
        #region Matching

        [Fact]
        public void Match1()
        {
            var src1 = @"
int x = 1; 
Console.WriteLine(1);
x++/*1A*/;
Console.WriteLine(2);

while (true)
{
    x++/*2A*/;
}

Console.WriteLine(1);
";
            var src2 = @"
int x = 1;
x++/*1B*/;
for (int i = 0; i < 10; i++) {}
y++;
if (x > 1)
{
    while (true)
    {
        x++/*2B*/;
    }

    Console.WriteLine(1);
}";
            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "int x = 1;", "int x = 1;" },
                { "int x = 1", "int x = 1" },
                { "x = 1", "x = 1" },
                { "Console.WriteLine(1);", "Console.WriteLine(1);" },
                { "x++/*1A*/;", "x++/*1B*/;" },
                { "Console.WriteLine(2);", "y++;" },
                { "while (true) {     x++/*2A*/; }", "while (true)     {         x++/*2B*/;     }" },
                { "{     x++/*2A*/; }", "{         x++/*2B*/;     }" },
                { "x++/*2A*/;", "x++/*2B*/;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void KnownMatches_Root()
        {
            string src1 = @"
Console.WriteLine(1);
";

            string src2 = @"
Console.WriteLine(2);
";

            var m1 = MakeMethodBody(src1);
            var m2 = MakeMethodBody(src2);

            var knownMatches = new[] { new KeyValuePair<SyntaxNode, SyntaxNode>(m1, m2) };
            var match = StatementSyntaxComparer.Default.ComputeMatch(m1, m2, knownMatches);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "Console.WriteLine(1);", "Console.WriteLine(2);" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void Locals_Rename()
        {
            var src1 = @"
int x = 1;
";
            var src2 = @"
int y = 1;
";
            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "int x = 1;", "int y = 1;" },
                { "int x = 1", "int y = 1" },
                { "x = 1", "y = 1" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void Locals_TypeChange()
        {
            var src1 = @"
int x = 1;
";
            var src2 = @"
byte x = 1;
";
            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "int x = 1;", "byte x = 1;" },
                { "int x = 1", "byte x = 1" },
                { "x = 1", "x = 1" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void BlocksWithLocals1()
        {
            var src1 = @"
{
    int a = 1;
}
{
    int b = 2;
}
";
            var src2 = @"
{
    int a = 3;
    int b = 4;
}
{
    int b = 5;
}
";
            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "{     int a = 1; }", "{     int a = 3;     int b = 4; }" },
                { "int a = 1;", "int a = 3;" },
                { "int a = 1", "int a = 3" },
                { "a = 1", "a = 3" },
                { "{     int b = 2; }", "{     int b = 5; }" },
                { "int b = 2;", "int b = 5;" },
                { "int b = 2", "int b = 5" },
                { "b = 2", "b = 5" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void IfBlocksWithLocals1()
        {
            var src1 = @"
if (X)
{
    int a = 1;
}
if (Y)
{
    int b = 2;
}
";
            var src2 = @"
if (Y)
{
    int a = 3;
    int b = 4;
}
if (X)
{
    int b = 5;
}
";
            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "if (X) {     int a = 1; }", "if (Y) {     int a = 3;     int b = 4; }" },
                { "{     int a = 1; }", "{     int a = 3;     int b = 4; }" },
                { "int a = 1;", "int a = 3;" },
                { "int a = 1", "int a = 3" },
                { "a = 1", "a = 3" },
                { "if (Y) {     int b = 2; }", "if (X) {     int b = 5; }" },
                { "{     int b = 2; }", "{     int b = 5; }" },
                { "int b = 2;", "int b = 5;" },
                { "int b = 2", "int b = 5" },
                { "b = 2", "b = 5" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void BlocksWithLocals2()
        {
            var src1 = @"
{
    int a = 1;
}
{
    {
        int b = 2;
    }
}
";
            var src2 = @"
{
    int b = 1;
}
{
    {
        int a = 2;
    }
}
";
            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "{     int a = 1; }", "{         int a = 2;     }" },
                { "int a = 1;", "int a = 2;" },
                { "int a = 1", "int a = 2" },
                { "a = 1", "a = 2" },
                { "{     {         int b = 2;     } }", "{     {         int a = 2;     } }" },
                { "{         int b = 2;     }", "{     int b = 1; }" },
                { "int b = 2;", "int b = 1;" },
                { "int b = 2", "int b = 1" },
                { "b = 2", "b = 1" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void BlocksWithLocals3()
        {
            var src1 = @"
{
    int a = 1, b = 2, c = 3;
    Console.WriteLine(a + b + c);
}
{
    int c = 4, b = 5, a = 6;
    Console.WriteLine(a + b + c);
}
{
    int a = 7, b = 8;
    Console.WriteLine(a + b);
}
";
            var src2 = @"
{
    int a = 9, b = 10;
    Console.WriteLine(a + b);
}
{
    int c = 11, b = 12, a = 13;
    Console.WriteLine(a + b + c);
}
{
    int a = 14, b = 15, c = 16;
    Console.WriteLine(a + b + c);
}
";
            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "{     int a = 1, b = 2, c = 3;     Console.WriteLine(a + b + c); }", "{     int a = 14, b = 15, c = 16;     Console.WriteLine(a + b + c); }" },
                { "int a = 1, b = 2, c = 3;", "int a = 14, b = 15, c = 16;" },
                { "int a = 1, b = 2, c = 3", "int a = 14, b = 15, c = 16" },
                { "a = 1", "a = 14" },
                { "b = 2", "b = 15" },
                { "c = 3", "c = 16" },
                { "Console.WriteLine(a + b + c);", "Console.WriteLine(a + b + c);" },
                { "{     int c = 4, b = 5, a = 6;     Console.WriteLine(a + b + c); }", "{     int c = 11, b = 12, a = 13;     Console.WriteLine(a + b + c); }" },
                { "int c = 4, b = 5, a = 6;", "int c = 11, b = 12, a = 13;" },
                { "int c = 4, b = 5, a = 6", "int c = 11, b = 12, a = 13" },
                { "c = 4", "c = 11" },
                { "b = 5", "b = 12" },
                { "a = 6", "a = 13" },
                { "Console.WriteLine(a + b + c);", "Console.WriteLine(a + b + c);" },
                { "{     int a = 7, b = 8;     Console.WriteLine(a + b); }", "{     int a = 9, b = 10;     Console.WriteLine(a + b); }" },
                { "int a = 7, b = 8;", "int a = 9, b = 10;" },
                { "int a = 7, b = 8", "int a = 9, b = 10" },
                { "a = 7", "a = 9" },
                { "b = 8", "b = 10" },
                { "Console.WriteLine(a + b);", "Console.WriteLine(a + b);" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchLambdas1()
        {
            var src1 = "Action x = a => a;";
            var src2 = "Action x = (a) => a;";

            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "Action x = a => a;", "Action x = (a) => a;" },
                { "Action x = a => a", "Action x = (a) => a" },
                { "x = a => a", "x = (a) => a" },
                { "a => a", "(a) => a" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchLambdas2a()
        {
            var src1 = @"
F(x => x + 1, 1, y => y + 1, delegate(int x) { return x; }, async u => u);
";
            var src2 = @"
F(y => y + 1, G(), x => x + 1, (int x) => x, u => u, async (u, v) => u + v);
";

            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "F(x => x + 1, 1, y => y + 1, delegate(int x) { return x; }, async u => u);", "F(y => y + 1, G(), x => x + 1, (int x) => x, u => u, async (u, v) => u + v);" },
                { "x => x + 1", "x => x + 1" },
                { "y => y + 1", "y => y + 1" },
                { "delegate(int x) { return x; }", "(int x) => x" },
                { "async u => u", "async (u, v) => u + v" },
            };

            expected.AssertEqual(actual);
        }

        [Fact, WorkItem(830419, "http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/830419")]
        public void MatchLambdas2b()
        {
            var src1 = @"
F(delegate { return x; });
";
            var src2 = @"
F((a) => x, () => x);
";

            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "F(delegate { return x; });", "F((a) => x, () => x);" },
                { "delegate { return x; }", "() => x" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchLambdas3()
        {
            var src1 = @"
a += async u => u;
";
            var src2 = @"
a += u => u;
";

            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "a += async u => u;", "a += u => u;" },
                { "async u => u", "u => u" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchLambdas4()
        {
            var src1 = @"
foreach (var a in z)
{
    var e = from q in a.Where(l => l > 10) select q + 1;
}
";
            var src2 = @"
foreach (var a in z)
{
    var e = from q in a.Where(l => l < 0) select q + 1;
}
";

            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "foreach (var a in z) {     var e = from q in a.Where(l => l > 10) select q + 1; }", "foreach (var a in z) {     var e = from q in a.Where(l => l < 0) select q + 1; }" },
                { "{     var e = from q in a.Where(l => l > 10) select q + 1; }", "{     var e = from q in a.Where(l => l < 0) select q + 1; }" },
                { "var e = from q in a.Where(l => l > 10) select q + 1;", "var e = from q in a.Where(l => l < 0) select q + 1;" },
                { "var e = from q in a.Where(l => l > 10) select q + 1", "var e = from q in a.Where(l => l < 0) select q + 1" },
                { "e = from q in a.Where(l => l > 10) select q + 1", "e = from q in a.Where(l => l < 0) select q + 1" },
                { "from q in a.Where(l => l > 10)", "from q in a.Where(l => l < 0)" },
                { "l => l > 10", "l => l < 0" },
                { "select q + 1", "select q + 1" },  // select clause
                { "select q + 1", "select q + 1" }   // query body
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchLambdas5()
        {
            var src1 = @"
F(a => b => c => d);
";
            var src2 = @"
F(a => b => c => d);
";

            var matches = GetMethodMatches(src1, src2);
            var actual = ToMatchingPairs(matches);

            var expected = new MatchingPairs
            {
                { "F(a => b => c => d);", "F(a => b => c => d);" },
                { "a => b => c => d", "a => b => c => d" },
                { "b => c => d", "b => c => d" },
                { "c => d", "c => d" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchLambdas6()
        {
            var src1 = @"
F(a => b => c => d);
";
            var src2 = @"
F(a => G(b => H(c => I(d))));
";

            var matches = GetMethodMatches(src1, src2);
            var actual = ToMatchingPairs(matches);

            var expected = new MatchingPairs
            {
                { "F(a => b => c => d);", "F(a => G(b => H(c => I(d))));" },
                { "a => b => c => d", "a => G(b => H(c => I(d)))" },
                { "b => c => d", "b => H(c => I(d))" },
                { "c => d", "c => I(d)" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchLambdas7()
        {
            var src1 = @"
F(a => 
{ 
    F(c => /*1*/d);
    F((u, v) => 
    {
        F((w) => c => /*2*/d);
        F(p => p);
    });
});
";
            var src2 = @"
F(a => 
{ 
    F(c => /*1*/d + 1);
    F((u, v) => 
    {
        F((w) => c => /*2*/d + 1);
        F(p => p*2);
    });
});
";

            var matches = GetMethodMatches(src1, src2);
            var actual = ToMatchingPairs(matches);

            var expected = new MatchingPairs
            {
                { "F(a =>  {      F(c => /*1*/d);     F((u, v) =>      {         F((w) => c => /*2*/d);         F(p => p);     }); });",
                  "F(a =>  {      F(c => /*1*/d + 1);     F((u, v) =>      {         F((w) => c => /*2*/d + 1);         F(p => p*2);     }); });" },
                { "a =>  {      F(c => /*1*/d);     F((u, v) =>      {         F((w) => c => /*2*/d);         F(p => p);     }); }",
                  "a =>  {      F(c => /*1*/d + 1);     F((u, v) =>      {         F((w) => c => /*2*/d + 1);         F(p => p*2);     }); }" },
                { "{      F(c => /*1*/d);     F((u, v) =>      {         F((w) => c => /*2*/d);         F(p => p);     }); }",
                  "{      F(c => /*1*/d + 1);     F((u, v) =>      {         F((w) => c => /*2*/d + 1);         F(p => p*2);     }); }" },
                { "F(c => /*1*/d);", "F(c => /*1*/d + 1);" },
                { "c => /*1*/d", "c => /*1*/d + 1" },
                { "F((u, v) =>      {         F((w) => c => /*2*/d);         F(p => p);     });", "F((u, v) =>      {         F((w) => c => /*2*/d + 1);         F(p => p*2);     });" },
                { "(u, v) =>      {         F((w) => c => /*2*/d);         F(p => p);     }", "(u, v) =>      {         F((w) => c => /*2*/d + 1);         F(p => p*2);     }" },
                { "{         F((w) => c => /*2*/d);         F(p => p);     }", "{         F((w) => c => /*2*/d + 1);         F(p => p*2);     }" },
                { "F((w) => c => /*2*/d);", "F((w) => c => /*2*/d + 1);" },
                { "(w) => c => /*2*/d", "(w) => c => /*2*/d + 1" },
                { "c => /*2*/d", "c => /*2*/d + 1" },
                { "F(p => p);", "F(p => p*2);" },
                { "p => p", "p => p*2" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchQueries1()
        {
            var src1 = @"
var q = from c in cars
        from ud in users_details
        from bd in bids
        select 1;
";
            var src2 = @"
var q = from c in cars
        from bd in bids
        from ud in users_details
        select 2;
";

            var match = GetMethodMatch(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "var q = from c in cars         from ud in users_details         from bd in bids         select 1;", "var q = from c in cars         from bd in bids         from ud in users_details         select 2;" },
                { "var q = from c in cars         from ud in users_details         from bd in bids         select 1", "var q = from c in cars         from bd in bids         from ud in users_details         select 2" },
                { "q = from c in cars         from ud in users_details         from bd in bids         select 1", "q = from c in cars         from bd in bids         from ud in users_details         select 2" },
                { "from c in cars", "from c in cars" },
                { "from ud in users_details         from bd in bids         select 1", "from bd in bids         from ud in users_details         select 2" },
                { "from ud in users_details", "from ud in users_details" },
                { "from bd in bids", "from bd in bids" },
                { "select 1", "select 2" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchQueries2()
        {
            var src1 = @"
var q = from c in cars
        from ud in users_details
        from bd in bids
        orderby c.listingOption descending
        where a.userID == ud.userid
        let images = from ai in auction_images
                     where ai.belongs_to == c.id
                     select ai
        let bid = (from b in bids
                    orderby b.id descending
                    where b.carID == c.id
                    select b.bidamount).FirstOrDefault()
        select bid;
";
            var src2 = @"
var q = from c in cars
        from ud in users_details
        from bd in bids
        orderby c.listingOption descending
        where a.userID == ud.userid
        let images = from ai in auction_images
                     where ai.belongs_to == c.id2
                     select ai + 1
        let bid = (from b in bids
                    orderby b.id ascending
                    where b.carID == c.id2
                    select b.bidamount).FirstOrDefault()
        select bid;
";

            var match = GetMethodMatches(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "var q = from c in cars         from ud in users_details         from bd in bids         orderby c.listingOption descending         where a.userID == ud.userid         let images = from ai in auction_images                      where ai.belongs_to == c.id                      select ai         let bid = (from b in bids                     orderby b.id descending                     where b.carID == c.id                     select b.bidamount).FirstOrDefault()         select bid;",
                  "var q = from c in cars         from ud in users_details         from bd in bids         orderby c.listingOption descending         where a.userID == ud.userid         let images = from ai in auction_images                      where ai.belongs_to == c.id2                      select ai + 1         let bid = (from b in bids                     orderby b.id ascending                     where b.carID == c.id2                     select b.bidamount).FirstOrDefault()         select bid;" },
                { "var q = from c in cars         from ud in users_details         from bd in bids         orderby c.listingOption descending         where a.userID == ud.userid         let images = from ai in auction_images                      where ai.belongs_to == c.id                      select ai         let bid = (from b in bids                     orderby b.id descending                     where b.carID == c.id                     select b.bidamount).FirstOrDefault()         select bid",
                  "var q = from c in cars         from ud in users_details         from bd in bids         orderby c.listingOption descending         where a.userID == ud.userid         let images = from ai in auction_images                      where ai.belongs_to == c.id2                      select ai + 1         let bid = (from b in bids                     orderby b.id ascending                     where b.carID == c.id2                     select b.bidamount).FirstOrDefault()         select bid" },
                { "q = from c in cars         from ud in users_details         from bd in bids         orderby c.listingOption descending         where a.userID == ud.userid         let images = from ai in auction_images                      where ai.belongs_to == c.id                      select ai         let bid = (from b in bids                     orderby b.id descending                     where b.carID == c.id                     select b.bidamount).FirstOrDefault()         select bid",
                  "q = from c in cars         from ud in users_details         from bd in bids         orderby c.listingOption descending         where a.userID == ud.userid         let images = from ai in auction_images                      where ai.belongs_to == c.id2                      select ai + 1         let bid = (from b in bids                     orderby b.id ascending                     where b.carID == c.id2                     select b.bidamount).FirstOrDefault()         select bid" },
                { "from c in cars", "from c in cars" },
                { "from ud in users_details         from bd in bids         orderby c.listingOption descending         where a.userID == ud.userid         let images = from ai in auction_images                      where ai.belongs_to == c.id                      select ai         let bid = (from b in bids                     orderby b.id descending                     where b.carID == c.id                     select b.bidamount).FirstOrDefault()         select bid", "from ud in users_details         from bd in bids         orderby c.listingOption descending         where a.userID == ud.userid         let images = from ai in auction_images                      where ai.belongs_to == c.id2                      select ai + 1         let bid = (from b in bids                     orderby b.id ascending                     where b.carID == c.id2                     select b.bidamount).FirstOrDefault()         select bid" },
                { "from ud in users_details", "from ud in users_details" },
                { "from bd in bids", "from bd in bids" },
                { "orderby c.listingOption descending", "orderby c.listingOption descending" },
                { "c.listingOption descending", "c.listingOption descending" },
                { "where a.userID == ud.userid", "where a.userID == ud.userid" },
                { "let images = from ai in auction_images                      where ai.belongs_to == c.id                      select ai",
                  "let images = from ai in auction_images                      where ai.belongs_to == c.id2                      select ai + 1" },
                { "from ai in auction_images", "from ai in auction_images" },
                { "where ai.belongs_to == c.id                      select ai", "where ai.belongs_to == c.id2                      select ai + 1" },
                { "where ai.belongs_to == c.id", "where ai.belongs_to == c.id2" },
                { "select ai", "select ai + 1" },
                { "let bid = (from b in bids                     orderby b.id descending                     where b.carID == c.id                     select b.bidamount).FirstOrDefault()",
                  "let bid = (from b in bids                     orderby b.id ascending                     where b.carID == c.id2                     select b.bidamount).FirstOrDefault()" },
                { "from b in bids", "from b in bids" },
                { "orderby b.id descending                     where b.carID == c.id                     select b.bidamount", "orderby b.id ascending                     where b.carID == c.id2                     select b.bidamount" },
                { "orderby b.id descending", "orderby b.id ascending" },
                { "b.id descending", "b.id ascending" },
                { "where b.carID == c.id", "where b.carID == c.id2" },
                { "select b.bidamount", "select b.bidamount" },
                { "select bid", "select bid" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchQueries3()
        {
            var src1 = @"
var q = from a in await seq1
        join c in await seq2 on F(u => u) equals G(s => s) into g1
        join l in await seq3 on F(v => v) equals G(t => t) into g2
        select a;

";
            var src2 = @"
var q = from a in await seq1
        join c in await seq2 on F(u => u + 1) equals G(s => s + 3) into g1
        join c in await seq3 on F(vv => vv + 2) equals G(tt => tt + 4) into g2
        select a + 1;
";

            var match = GetMethodMatches(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "var q = from a in await seq1         join c in await seq2 on F(u => u) equals G(s => s) into g1         join l in await seq3 on F(v => v) equals G(t => t) into g2         select a;", "var q = from a in await seq1         join c in await seq2 on F(u => u + 1) equals G(s => s + 3) into g1         join c in await seq3 on F(vv => vv + 2) equals G(tt => tt + 4) into g2         select a + 1;" },
                { "var q = from a in await seq1         join c in await seq2 on F(u => u) equals G(s => s) into g1         join l in await seq3 on F(v => v) equals G(t => t) into g2         select a", "var q = from a in await seq1         join c in await seq2 on F(u => u + 1) equals G(s => s + 3) into g1         join c in await seq3 on F(vv => vv + 2) equals G(tt => tt + 4) into g2         select a + 1" },
                { "q = from a in await seq1         join c in await seq2 on F(u => u) equals G(s => s) into g1         join l in await seq3 on F(v => v) equals G(t => t) into g2         select a", "q = from a in await seq1         join c in await seq2 on F(u => u + 1) equals G(s => s + 3) into g1         join c in await seq3 on F(vv => vv + 2) equals G(tt => tt + 4) into g2         select a + 1" },
                { "from a in await seq1", "from a in await seq1" },
                { "await seq1", "await seq1" },
                { "join c in await seq2 on F(u => u) equals G(s => s) into g1         join l in await seq3 on F(v => v) equals G(t => t) into g2         select a", "join c in await seq2 on F(u => u + 1) equals G(s => s + 3) into g1         join c in await seq3 on F(vv => vv + 2) equals G(tt => tt + 4) into g2         select a + 1" },
                { "join c in await seq2 on F(u => u) equals G(s => s) into g1", "join c in await seq2 on F(u => u + 1) equals G(s => s + 3) into g1" },
                { "await seq2", "await seq2" },
                { "u => u", "u => u + 1" },
                { "s => s", "s => s + 3" },
                { "into g1", "into g1" },
                { "join l in await seq3 on F(v => v) equals G(t => t) into g2", "join c in await seq3 on F(vv => vv + 2) equals G(tt => tt + 4) into g2" },
                { "await seq3", "await seq3" },
                { "v => v", "vv => vv + 2" },
                { "t => t", "tt => tt + 4" },
                { "into g2", "into g2" },
                { "select a", "select a + 1" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchQueries4()
        {
            var src1 = "F(from a in await b from x in y select c);";
            var src2 = "F(from a in await c from x in y select c);";

            var match = GetMethodMatches(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "F(from a in await b from x in y select c);", "F(from a in await c from x in y select c);" },
                { "from a in await b", "from a in await c" },
                { "await b", "await c" },
                { "from x in y select c", "from x in y select c" },
                { "from x in y", "from x in y" },
                { "select c", "select c" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchQueries5()
        {
            var src1 = "F(from a in b  group a by a.x into g  select g);";
            var src2 = "F(from a in b  group z by z.y into h  select h);";

            var match = GetMethodMatches(src1, src2);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "F(from a in b  group a by a.x into g  select g);", "F(from a in b  group z by z.y into h  select h);" },
                { "from a in b", "from a in b" },
                { "group a by a.x into g  select g", "group z by z.y into h  select h" },
                { "group a by a.x", "group z by z.y" },
                { "into g  select g", "into h  select h" },
                { "select g", "select h" },
                { "select g", "select h" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchYields()
        {
            var src1 = @"
yield return /*1*/ 1;

{
    yield return /*2*/ 2;
}

foreach (var x in y) { yield return /*3*/ 3; }
";
            var src2 = @"
yield return /*1*/ 1;
yield return /*2*/ 3;
foreach (var x in y) { yield return /*3*/ 2; }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Iterator);
            var actual = ToMatchingPairs(match);

            // note that yield returns are matched in source order, regardless of the yielded expressions:
            var expected = new MatchingPairs
            {
                { "yield return /*1*/ 1;", "yield return /*1*/ 1;" },
                { "{     yield return /*2*/ 2; }", "{ yield return /*3*/ 2; }" },
                { "yield return /*2*/ 2;", "yield return /*2*/ 3;" },
                { "foreach (var x in y) { yield return /*3*/ 3; }", "foreach (var x in y) { yield return /*3*/ 2; }" },
                { "yield return /*3*/ 3;", "yield return /*3*/ 2;" },
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void KnownMatches()
        {
            string src1 = @"
Console.WriteLine(1)/*1*/;
Console.WriteLine(1)/*2*/;
";

            string src2 = @"
Console.WriteLine(1)/*3*/;
Console.WriteLine(1)/*4*/;
";

            var m1 = MakeMethodBody(src1);
            var m2 = MakeMethodBody(src2);

            var knownMatches = new KeyValuePair<SyntaxNode, SyntaxNode>[]
            {
                new KeyValuePair<SyntaxNode, SyntaxNode>(m1.Statements[1], m2.Statements[0])
            };

            // pre-matched:

            var match = StatementSyntaxComparer.Default.ComputeMatch(m1, m2, knownMatches);

            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "Console.WriteLine(1)/*1*/;", "Console.WriteLine(1)/*4*/;" },
                { "Console.WriteLine(1)/*2*/;", "Console.WriteLine(1)/*3*/;" }
            };

            expected.AssertEqual(actual);

            // not pre-matched:

            match = StatementSyntaxComparer.Default.ComputeMatch(m1, m2);

            actual = ToMatchingPairs(match);

            expected = new MatchingPairs
            {
                { "Console.WriteLine(1)/*1*/;", "Console.WriteLine(1)/*3*/;" },
                { "Console.WriteLine(1)/*2*/;", "Console.WriteLine(1)/*4*/;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchConstructorWithInitializer1()
        {
            var src1 = @"
(int x = 1) : base(a => a + 1) { Console.WriteLine(1); }
";
            var src2 = @"
(int x = 1) : base(a => a + 1) { Console.WriteLine(1); }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.ConstructorWithParameters);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "a => a + 1", "a => a + 1" },
                { "{ Console.WriteLine(1); }", "{ Console.WriteLine(1); }" },
                { "Console.WriteLine(1);", "Console.WriteLine(1);" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchConstructorWithInitializer2()
        {
            var src1 = @"
() : base(a => a + 1) { Console.WriteLine(1); }
";
            var src2 = @"
() { Console.WriteLine(1); }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.ConstructorWithParameters);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "{ Console.WriteLine(1); }", "{ Console.WriteLine(1); }" },
                { "Console.WriteLine(1);", "Console.WriteLine(1);" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchExceptionHandlers()
        {
            var src1 = @"
try { throw new InvalidOperationException(1); }
catch (IOException e) when (filter(e)) { Console.WriteLine(2); }
catch (Exception e) when (filter(e)) { Console.WriteLine(3); }
";
            var src2 = @"
try { throw new InvalidOperationException(10); }
catch (IOException e) when (filter(e)) { Console.WriteLine(20); }
catch (Exception e) when (filter(e)) { Console.WriteLine(30); }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "try { throw new InvalidOperationException(1); } catch (IOException e) when (filter(e)) { Console.WriteLine(2); } catch (Exception e) when (filter(e)) { Console.WriteLine(3); }", "try { throw new InvalidOperationException(10); } catch (IOException e) when (filter(e)) { Console.WriteLine(20); } catch (Exception e) when (filter(e)) { Console.WriteLine(30); }" },
                { "{ throw new InvalidOperationException(1); }", "{ throw new InvalidOperationException(10); }" },
                { "throw new InvalidOperationException(1);", "throw new InvalidOperationException(10);" },
                { "catch (IOException e) when (filter(e)) { Console.WriteLine(2); }", "catch (IOException e) when (filter(e)) { Console.WriteLine(20); }" },
                { "(IOException e)", "(IOException e)" },
                { "when (filter(e))", "when (filter(e))" },
                { "{ Console.WriteLine(2); }", "{ Console.WriteLine(20); }" },
                { "Console.WriteLine(2);", "Console.WriteLine(20);" },
                { "catch (Exception e) when (filter(e)) { Console.WriteLine(3); }", "catch (Exception e) when (filter(e)) { Console.WriteLine(30); }" },
                { "(Exception e)", "(Exception e)" },
                { "when (filter(e))", "when (filter(e))" },
                { "{ Console.WriteLine(3); }", "{ Console.WriteLine(30); }" },
                { "Console.WriteLine(3);", "Console.WriteLine(30);" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchTuple()
        {
            var src1 = @"
return (1, 2);
return (d, 6);
return (10, e, 22);
return (2, () => { 
    int a = 6;
    return 1;
});";

            var src2 = @"
return (1, 2, 3);
return (d, 5);
return (10, e);
return (2, () => {
    int a = 6;
    return 5;
});";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "return (1, 2);", "return (1, 2, 3);" },
                { "return (d, 6);", "return (d, 5);" },
                { "return (10, e, 22);", "return (10, e);" },
                { "return (2, () => {      int a = 6;     return 1; });", "return (2, () => {     int a = 6;     return 5; });" },
                { "() => {      int a = 6;     return 1; }", "() => {     int a = 6;     return 5; }" },
                { "{      int a = 6;     return 1; }", "{     int a = 6;     return 5; }" },
                { "int a = 6;", "int a = 6;" },
                { "int a = 6", "int a = 6" },
                { "a = 6", "a = 6" },
                { "return 1;", "return 5;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchLocalFunctionDefinitions()
        {
            var src1 = @"
(int a, string c) F1(int i) { return null; }
(int a, int b) F2(int i) { return null; }
(int a, int b, int c) F3(int i) { return null; }
";

            var src2 = @"
(int a, int b) F1(int i) { return null; }
(int a, int b, string c) F2(int i) { return null; }
(int a, int b) F3(int i) { return null; }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "(int a, string c) F1(int i) { return null; }", "(int a, int b) F1(int i) { return null; }" },
                { "{ return null; }", "{ return null; }" },
                { "return null;", "return null;" },
                { "(int a, int b) F2(int i) { return null; }", "(int a, int b, string c) F2(int i) { return null; }" },
                { "{ return null; }", "{ return null; }" },
                { "return null;", "return null;" },
                { "(int a, int b, int c) F3(int i) { return null; }", "(int a, int b) F3(int i) { return null; }" },
                { "{ return null; }", "{ return null; }" },
                { "return null;", "return null;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchVariableDesignations()
        {
            var src1 = @"
M(out int z);
N(out var a);
";

            var src2 = @"
M(out var z);
N(out var b);
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "M(out int z);", "M(out var z);" },
                { "z", "z" },
                { "N(out var a);", "N(out var b);" },
                { "a", "b" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchParenthesizedVariable_Update()
        {
            var src1 = @"
var (x1, (x2, x3)) = (1, (2, true));
var (a1, a2) = (1, () => { return 7; });
";

            var src2 = @"
var (x1, (x3, x4)) = (1, (2, true));
var (a1, a3) = (1, () => { return 8; });
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "var (x1, (x2, x3)) = (1, (2, true));", "var (x1, (x3, x4)) = (1, (2, true));" },
                { "x1", "x1" },
                { "x2", "x4" },
                { "x3", "x3" },
                { "var (a1, a2) = (1, () => { return 7; });", "var (a1, a3) = (1, () => { return 8; });" },
                { "a1", "a1" },
                { "a2", "a3" },
                { "() => { return 7; }", "() => { return 8; }" },
                { "{ return 7; }", "{ return 8; }" },
                { "return 7;", "return 8;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchParenthesizedVariable_Insert()
        {
            var src1 = @"var (z1, z2) = (1, 2);";
            var src2 = @"var (z1, z2, z3) = (1, 2, 5);";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "var (z1, z2) = (1, 2);", "var (z1, z2, z3) = (1, 2, 5);" },
                { "z1", "z1" },
                { "z2", "z2" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchParenthesizedVariable_Delete()
        {
            var src1 = @"var (y1, y2, y3) = (1, 2, 7);";
            var src2 = @"var (y1, y2) = (1, 4);";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "var (y1, y2, y3) = (1, 2, 7);", "var (y1, y2) = (1, 4);" },
                { "y1", "y1" },
                { "y2", "y2" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchForeachVariable_Update1()
        {
            var src1 = @"
foreach (var (a1, a2) in e) { A1(); }
foreach ((var b1, var b2) in e) { A2(); }
";

            var src2 = @"
foreach (var (a1, a3) in e) { A1(); }
foreach ((var b3, int b2) in e) { A2(); }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "foreach (var (a1, a2) in e) { A1(); }", "foreach (var (a1, a3) in e) { A1(); }" },
                { "a1", "a1" },
                { "a2", "a3" },
                { "{ A1(); }", "{ A1(); }" },
                { "A1();", "A1();" },
                { "foreach ((var b1, var b2) in e) { A2(); }", "foreach ((var b3, int b2) in e) { A2(); }" },
                { "b1", "b3" },
                { "b2", "b2" },
                { "{ A2(); }", "{ A2(); }" },
                { "A2();", "A2();" },
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchForeachVariable_Update2()
        {
            var src1 = @"
foreach (_ in e2) { yield return 4; }
foreach (_ in e3) { A(); }
";

            var src2 = @"
foreach (_ in e4) { A(); }
foreach (var b in e2) { yield return 4; }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "foreach (_ in e2) { yield return 4; }", "foreach (var b in e2) { yield return 4; }" },
                { "{ yield return 4; }", "{ yield return 4; }" },
                { "yield return 4;", "yield return 4;" },
                { "foreach (_ in e3) { A(); }", "foreach (_ in e4) { A(); }" },
                { "{ A(); }", "{ A(); }" },
                { "A();", "A();" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchForeachVariable_Insert()
        {
            var src1 = @"
foreach (var (a3, a4) in e) { }
foreach ((var b4, var b5) in e) { }
";

            var src2 = @"
foreach (var (a3, a5, a4) in e) { }
foreach ((var b6, var b4, var b5) in e) { }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "foreach (var (a3, a4) in e) { }", "foreach (var (a3, a5, a4) in e) { }" },
                { "a3", "a3" },
                { "a4", "a4" },
                { "{ }", "{ }" },
                { "foreach ((var b4, var b5) in e) { }", "foreach ((var b6, var b4, var b5) in e) { }" },
                { "b4", "b4" },
                { "b5", "b5" },
                { "{ }", "{ }" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchForeachVariable_Delete()
        {
            var src1 = @"
foreach (var (a11, a12, a13) in e) { A1(); }
foreach ((var b7, var b8, var b9) in e) { A2(); }
";

            var src2 = @"
foreach (var (a12, a13) in e1) { A1(); }
foreach ((var b7, var b9) in e) { A2(); }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "foreach (var (a11, a12, a13) in e) { A1(); }", "foreach (var (a12, a13) in e1) { A1(); }" },
                { "a12", "a12" },
                { "a13", "a13" },
                { "{ A1(); }", "{ A1(); }" },
                { "A1();", "A1();" },
                { "foreach ((var b7, var b8, var b9) in e) { A2(); }", "foreach ((var b7, var b9) in e) { A2(); }" },
                { "b7", "b7" },
                { "b9", "b9" },
                { "{ A2(); }", "{ A2(); }" },
                { "A2();", "A2();" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchConstantPattern()
        {
            var src1 = @"
if ((o is null) && (y == 7)) return 3;
if (a is 7) return 5;
";

            var src2 = @"
if ((o1 is null) && (y == 7)) return 3;
if (a is 77) return 5;
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs {
                { "if ((o is null) && (y == 7)) return 3;", "if ((o1 is null) && (y == 7)) return 3;" },
                { "return 3;", "return 3;" },
                { "if (a is 7) return 5;", "if (a is 77) return 5;" },
                { "return 5;", "return 5;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchDeclarationPattern()
        {
            var src1 = @"
if (!(o is int i) && (y == 7)) return;
if (!(a is string s)) return;
if (!(b is string t)) return;
if (!(c is int j)) return;
";

            var src2 = @"
if (!(b is string t1)) return;
if (!(o1 is int i) && (y == 7)) return;
if (!(c is int)) return;
if (!(a is int s)) return;
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs {
                { "if (!(o is int i) && (y == 7)) return;", "if (!(o1 is int i) && (y == 7)) return;" },
                { "i", "i" },
                { "return;", "return;" },
                { "if (!(a is string s)) return;", "if (!(a is int s)) return;" },
                { "s", "s" },
                { "return;", "return;" },
                { "if (!(b is string t)) return;", "if (!(b is string t1)) return;" },
                { "t", "t1" },
                { "return;", "return;" },
                { "if (!(c is int j)) return;", "if (!(c is int)) return;" },
                { "return;", "return;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchCasePattern_UpdateInsert()
        {
            var src1 = @"
switch(shape)
{
    case Circle c: return 1;
    default: return 4;
}
";

            var src2 = @"
switch(shape)
{
    case Circle c1: return 1;
    case Point p: return 0;
    default: return 4;
}
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs {
                { "switch(shape) {     case Circle c: return 1;     default: return 4; }", "switch(shape) {     case Circle c1: return 1;     case Point p: return 0;     default: return 4; }" },
                { "case Circle c: return 1;", "case Circle c1: return 1;" },
                { "case Circle c:", "case Circle c1:" },
                { "c", "c1" },
                { "return 1;", "return 1;" },
                { "default: return 4;", "default: return 4;" },
                { "return 4;", "return 4;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchWhenCondition()
        {
            var src1 = @"
switch(shape)
{
    case Circle c when (c < 10): return 1;
    case Circle c when (c > 100): return 2;
}
";

            var src2 = @"
switch(shape)
{
    case Circle c when (c < 5): return 1;
    case Circle c2 when (c2 > 100): return 2;
}
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "switch(shape) {     case Circle c when (c < 10): return 1;     case Circle c when (c > 100): return 2; }", "switch(shape) {     case Circle c when (c < 5): return 1;     case Circle c2 when (c2 > 100): return 2; }" },
                { "case Circle c when (c < 10): return 1;", "case Circle c when (c < 5): return 1;" },
                { "case Circle c when (c < 10):", "case Circle c when (c < 5):" },
                { "c", "c" },
                { "when (c < 10)", "when (c < 5)" },
                { "return 1;", "return 1;" },
                { "case Circle c when (c > 100): return 2;", "case Circle c2 when (c2 > 100): return 2;" },
                { "case Circle c when (c > 100):", "case Circle c2 when (c2 > 100):" },
                { "c", "c2" },
                { "when (c > 100)", "when (c2 > 100)" },
                { "return 2;", "return 2;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchRef()
        {
            var src1 = @"
ref int a = ref G(new int[] { 1, 2 });
    ref int G(int[] p)
    {
        return ref p[1];
    }
";

            var src2 = @"
ref int32 a = ref G1(new int[] { 1, 2 });
    ref int G1(int[] p)
    {
        return ref p[2];
    }
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "ref int a = ref G(new int[] { 1, 2 });", "ref int32 a = ref G1(new int[] { 1, 2 });" },
                { "ref int a = ref G(new int[] { 1, 2 })", "ref int32 a = ref G1(new int[] { 1, 2 })" },
                { "a = ref G(new int[] { 1, 2 })", "a = ref G1(new int[] { 1, 2 })" },
                { "ref int G(int[] p)     {         return ref p[1];     }", "ref int G1(int[] p)     {         return ref p[2];     }" },
                { "{         return ref p[1];     }", "{         return ref p[2];     }" },
                { "return ref p[1];", "return ref p[2];" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchThrowException_UpdateInsert()
        {
            var src1 = @"
return a > 3 ? a : throw new Exception();
return c > 7 ? c : 7;
";

            var src2 = @"
return a > 3 ? a : throw new ArgumentException();
return c > 7 ? c : throw new IndexOutOfRangeException();
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "return a > 3 ? a : throw new Exception();", "return a > 3 ? a : throw new ArgumentException();" },
                { "return c > 7 ? c : 7;", "return c > 7 ? c : throw new IndexOutOfRangeException();" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void MatchThrowException_UpdateDelete()
        {
            var src1 = @"
return a > 3 ? a : throw new Exception();
return b > 5 ? b : throw new OperationCanceledException();
";

            var src2 = @"
return a > 3 ? a : throw new ArgumentException();
return b > 5 ? b : 5;
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs
            {
                { "return a > 3 ? a : throw new Exception();", "return a > 3 ? a : throw new ArgumentException();" },
                { "return b > 5 ? b : throw new OperationCanceledException();", "return b > 5 ? b : 5;" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void StringLiteral_update()
        {
            var src1 = @"
var x = ""Hello1"";
";
            var src2 = @"
var x = ""Hello2"";
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [x = \"Hello1\"]@8 -> [x = \"Hello2\"]@8");
        }

        [Fact]
        public void InterpolatedStringText_update()
        {
            var src1 = @"
var x = $""Hello1"";
";
            var src2 = @"
var x = $""Hello2"";
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [x = $\"Hello1\"]@8 -> [x = $\"Hello2\"]@8");
        }

        [Fact]
        public void Interpolation_update()
        {
            var src1 = @"
var x = $""Hello{123}"";
";
            var src2 = @"
var x = $""Hello{124}"";
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [x = $\"Hello{123}\"]@8 -> [x = $\"Hello{124}\"]@8");
        }

        [Fact]
        public void InterpolationFormatClause_update()
        {
            var src1 = @"
var x = $""Hello{123:N1}"";
";
            var src2 = @"
var x = $""Hello{123:N2}"";
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [x = $\"Hello{123:N1}\"]@8 -> [x = $\"Hello{123:N2}\"]@8");
        }

        [WorkItem(18970, "https://github.com/dotnet/roslyn/issues/18970")]
        [Fact]
        public void MatchCasePattern_UpdateDelete()
        {
            var src1 = @"
switch(shape)
{
    case Point p: return 0;
    case Circle c: return 1;
}
";

            var src2 = @"
switch(shape)
{
    case Circle circle: return 1;
}
";

            var match = GetMethodMatches(src1, src2, kind: MethodKind.Regular);
            var actual = ToMatchingPairs(match);

            var expected = new MatchingPairs {
                { "switch(shape) {     case Point p: return 0;     case Circle c: return 1; }", "switch(shape) {     case Circle circle: return 1; }" },
                { "p", "circle" },
                { "case Circle c: return 1;", "case Circle circle: return 1;" },
                { "case Circle c:", "case Circle circle:" },
                { "return 1;", "return 1;" }
            };

            expected.AssertEqual(actual);
        }

        #endregion

        #region Variable Declaration

        [Fact]
        public void VariableDeclaration_Insert()
        {
            var src1 = "if (x == 1) { x++; }";
            var src2 = "var x = 1; if (x == 1) { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [var x = 1;]@2",
                "Insert [var x = 1]@2",
                "Insert [x = 1]@6");
        }

        [Fact]
        public void VariableDeclaration_Update()
        {
            var src1 = "int x = F(1), y = G(2);";
            var src2 = "int x = F(3), y = G(4);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [x = F(1)]@6 -> [x = F(3)]@6",
                "Update [y = G(2)]@16 -> [y = G(4)]@16");
        }

        [Fact]
        public void ParenthesizedVariableDeclaration_Update()
        {
            var src1 = @"
var (x1, (x2, x3)) = (1, (2, true));
var (a1, a2) = (1, () => { return 7; });
";
           var src2 = @"
var (x1, (x2, x4)) = (1, (2, true));
var (a1, a3) = (1, () => { return 8; });
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [x3]@18 -> [x4]@18",
                "Update [a2]@51 -> [a3]@51");
        }

        [Fact]
        public void ParenthesizedVariableDeclaration_Insert()
        {
            var src1 = @"var (z1, z2) = (1, 2);";
            var src2 = @"var (z1, z2, z3) = (1, 2, 5);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [var (z1, z2) = (1, 2);]@2 -> [var (z1, z2, z3) = (1, 2, 5);]@2",
                "Insert [z3]@15");
        }

        [Fact]
        public void ParenthesizedVariableDeclaration_Delete()
        {
            var src1 = @"var (y1, y2, y3) = (1, 2, 7);";
            var src2 = @"var (y1, y2) = (1, 4);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [var (y1, y2, y3) = (1, 2, 7);]@2 -> [var (y1, y2) = (1, 4);]@2",
                "Delete [y3]@15");
        }

        [Fact]
        public void VariableDeclaraions_Reorder()
        {
            var src1 = @"var (a, b) = (1, 2); var (c, d) = (3, 4);";
            var src2 = @"var (c, d) = (3, 4); var (a, b) = (1, 2);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [var (c, d) = (3, 4);]@23 -> @2");
        }

        [Fact]
        public void VariableNames_Reorder()
        {
            var src1 = @"var (a, b) = (1, 2);";
            var src2 = @"var (b, a) = (2, 1);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [var (a, b) = (1, 2);]@2 -> [var (b, a) = (2, 1);]@2",
                "Reorder [b]@10 -> @7");
        }

        [Fact]
        public void VariableNamesAndDeclaraions_Reorder()
        {
            var src1 = @"var (a, b) = (1, 2); var (c, d) = (3, 4);";
            var src2 = @"var (d, c) = (3, 4); var (a, b) = (1, 2);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [var (c, d) = (3, 4);]@23 -> @2",
                "Reorder [d]@31 -> @7");
        }

        [Fact]
        public void ParenthesizedVariableDeclaration_Reorder()
        {
            var src1 = @"var (a, (b, c)) = (1, (2, 3));";
            var src2 = @"var ((b, c), a) = ((2, 3), 1);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [var (a, (b, c)) = (1, (2, 3));]@2 -> [var ((b, c), a) = ((2, 3), 1);]@2",
                "Reorder [a]@7 -> @15");
        }

        [Fact]
        public void ParenthesizedVariableDeclaration_DoubleReorder()
        {
            var src1 = @"var (a, (b, c)) = (1, (2, 3));";
            var src2 = @"var ((c, b), a) = ((2, 3), 1);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [var (a, (b, c)) = (1, (2, 3));]@2 -> [var ((c, b), a) = ((2, 3), 1);]@2",
                "Reorder [b]@11 -> @11",
                "Reorder [c]@14 -> @8");
        }

        [Fact]
        public void ParenthesizedVariableDeclaration_ComplexReorder()
        {
            var src1 = @"var (a, (b, c)) = (1, (2, 3)); var (x, (y, z)) = (4, (5, 6));";
            var src2 = @"var (x, (y, z)) = (4, (5, 6)); var ((c, b), a) = (1, (2, 3)); ";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [var (x, (y, z)) = (4, (5, 6));]@33 -> @2",
                "Update [var (a, (b, c)) = (1, (2, 3));]@2 -> [var ((c, b), a) = (1, (2, 3));]@33",
                "Reorder [b]@11 -> @42",
                "Reorder [c]@14 -> @39");
        }

        #endregion

        #region Switch

        [Fact]
        public void Switch1()
        {
            var src1 = "switch (a) { case 1: f(); break; } switch (b) { case 2: g(); break; }";
            var src2 = "switch (b) { case 2: f(); break; } switch (a) { case 1: g(); break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [switch (b) { case 2: g(); break; }]@37 -> @2",
                "Update [case 1: f(); break;]@15 -> [case 2: f(); break;]@15",
                "Move [case 1: f(); break;]@15 -> @15",
                "Update [case 2: g(); break;]@50 -> [case 1: g(); break;]@50",
                "Move [case 2: g(); break;]@50 -> @50");
        }

        [Fact]
        public void Switch_Case_Reorder()
        {
            var src1 = "switch (expr) { case 1: f(); break;   case 2: case 3: case 4: g(); break; }";
            var src2 = "switch (expr) { case 2: case 3: case 4: g(); break;   case 1: f(); break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [case 2: case 3: case 4: g(); break;]@40 -> @18");
        }

        [Fact]
        public void Switch_Case_Update()
        {
            var src1 = "switch (expr) { case 1: f(); break; }";
            var src2 = "switch (expr) { case 2: f(); break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [case 1: f(); break;]@18 -> [case 2: f(); break;]@18");
        }

        [WorkItem(18970, "https://github.com/dotnet/roslyn/issues/18970")]
        [Fact]
        public void CasePatternLabel_UpdateDelete()
        {
            var src1 = @"
switch(shape)
{
    case Point p: return 0;
    case Circle c: return 1;
}
";

            var src2 = @"
switch(shape)
{
    case Circle circle: return 1;
}
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [case Circle c: return 1;]@55 -> [case Circle circle: return 1;]@26",
                "Update [p]@37 -> [circle]@38",
                "Move [p]@37 -> @38",
                "Delete [case Point p: return 0;]@26",
                "Delete [case Point p:]@26",
                "Delete [return 0;]@40",
                "Delete [c]@67");
        }

        #endregion

        #region Try Catch Finally

        [Fact]
        public void TryInsert1()
        {
            var src1 = "x++;";
            var src2 = "try { x++; } catch { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [try { x++; } catch { }]@2",
                "Insert [{ x++; }]@6",
                "Insert [catch { }]@15",
                "Move [x++;]@2 -> @8",
                "Insert [{ }]@21");
        }

        [Fact]
        public void TryInsert2()
        {
            var src1 = "{ x++; }";
            var src2 = "try { x++; } catch { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [try { x++; } catch { }]@2",
                "Move [{ x++; }]@2 -> @6",
                "Insert [catch { }]@15",
                "Insert [{ }]@21");
        }

        [Fact]
        public void TryDelete1()
        {
            var src1 = "try { x++; } catch { }";
            var src2 = "x++;";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [x++;]@8 -> @2",
                "Delete [try { x++; } catch { }]@2",
                "Delete [{ x++; }]@6",
                "Delete [catch { }]@15",
                "Delete [{ }]@21");
        }

        [Fact]
        public void TryDelete2()
        {
            var src1 = "try { x++; } catch { }";
            var src2 = "{ x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ x++; }]@6 -> @2",
                "Delete [try { x++; } catch { }]@2",
                "Delete [catch { }]@15",
                "Delete [{ }]@21");
        }

        [Fact]
        public void TryReorder()
        {
            var src1 = "try { x++; } catch { /*1*/ } try { y++; } catch { /*2*/ }";
            var src2 = "try { y++; } catch { /*2*/ } try { x++; } catch { /*1*/ } ";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [try { y++; } catch { /*2*/ }]@31 -> @2");
        }

        [Fact]
        public void Finally_DeleteHeader()
        {
            var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } finally { /*3*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ /*3*/ }]@47 -> @39",
                "Delete [finally { /*3*/ }]@39");
        }

        [Fact]
        public void Finally_InsertHeader()
        {
            var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } finally { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [finally { /*3*/ }]@39",
                "Move [{ /*3*/ }]@39 -> @47");
        }

        [Fact]
        public void CatchUpdate()
        {
            var src1 = "try { } catch (Exception e) { }";
            var src2 = "try { } catch (IOException e) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [(Exception e)]@16 -> [(IOException e)]@16");
        }

        [Fact]
        public void CatchInsert()
        {
            var src1 = "try { /*1*/ } catch (Exception e) { /*2*/ } ";
            var src2 = "try { /*1*/ } catch (IOException e) { /*3*/ } catch (Exception e) { /*2*/ } ";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [catch (IOException e) { /*3*/ }]@16",
                "Insert [(IOException e)]@22",
                "Insert [{ /*3*/ }]@38");
        }

        [Fact]
        public void CatchBodyUpdate()
        {
            var src1 = "try { } catch (E e) { x++; }";
            var src2 = "try { } catch (E e) { y++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [x++;]@24 -> [y++;]@24");
        }

        [Fact]
        public void CatchDelete()
        {
            var src1 = "try { } catch (IOException e) { } catch (Exception e) { } ";
            var src2 = "try { } catch (IOException e) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [catch (Exception e) { }]@36",
                "Delete [(Exception e)]@42",
                "Delete [{ }]@56");
        }

        [Fact]
        public void CatchReorder1()
        {
            var src1 = "try { } catch (IOException e) { } catch (Exception e) { } ";
            var src2 = "try { } catch (Exception e) { } catch (IOException e) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [catch (Exception e) { }]@36 -> @10");
        }

        [Fact]
        public void CatchReorder2()
        {
            var src1 = "try { } catch (IOException e) { } catch (Exception e) { } catch { }";
            var src2 = "try { } catch (A e) { } catch (Exception e) { } catch (IOException e) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [catch (Exception e) { }]@36 -> @26",
                "Reorder [catch { }]@60 -> @10",
                "Insert [(A e)]@16");
        }

        [Fact]
        public void CatchFilterReorder2()
        {
            var src1 = "try { } catch (Exception e) when (e != null) { } catch (Exception e) { } catch { }";
            var src2 = "try { } catch when (s == 1) { } catch (Exception e) { } catch (Exception e) when (e != null) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [catch (Exception e) { }]@51 -> @34",
                "Reorder [catch { }]@75 -> @10",
                "Insert [when (s == 1)]@16");
        }

        [Fact]
        public void CatchInsertDelete()
        {
            var src1 = @"
try { x++; } catch (E e) { /*1*/ } catch (Exception e) { /*2*/ } 
try { Console.WriteLine(); } finally { /*3*/ }";

            var src2 = @"
try { x++; } catch (Exception e) { /*2*/ }  
try { Console.WriteLine(); } catch (E e) { /*1*/ } finally { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [catch (E e) { /*1*/ }]@79",
                "Insert [(E e)]@85",
                "Move [{ /*1*/ }]@29 -> @91",
                "Delete [catch (E e) { /*1*/ }]@17",
                "Delete [(E e)]@23");
        }

        [Fact]
        public void Catch_DeleteHeader1()
        {
            var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } catch (E2 e) { /*3*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ /*3*/ }]@52 -> @39",
                "Delete [catch (E2 e) { /*3*/ }]@39",
                "Delete [(E2 e)]@45");
        }

        [Fact]
        public void Catch_InsertHeader1()
        {
            var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } catch (E2 e) { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [catch (E2 e) { /*3*/ }]@39",
                "Insert [(E2 e)]@45",
                "Move [{ /*3*/ }]@39 -> @52");
        }

        [Fact]
        public void Catch_DeleteHeader2()
        {
            var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } catch { /*3*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ /*3*/ }]@45 -> @39",
                "Delete [catch { /*3*/ }]@39");
        }

        [Fact]
        public void Catch_InsertHeader2()
        {
            var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ } { /*3*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ } catch { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [catch { /*3*/ }]@39",
                "Move [{ /*3*/ }]@39 -> @45");
        }

        [Fact]
        public void Catch_InsertFilter1()
        {
            var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [when (e == null)]@29");
        }

        [Fact]
        public void Catch_InsertFilter2()
        {
            var src1 = "try { /*1*/ } catch when (e == null) { /*2*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [(E1 e)]@22");
        }

        [Fact]
        public void Catch_InsertFilter3()
        {
            var src1 = "try { /*1*/ } catch { /*2*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [(E1 e)]@22",
                "Insert [when (e == null)]@29");
        }

        [Fact]
        public void Catch_DeleteDeclaration1()
        {
            var src1 = "try { /*1*/ } catch (E1 e) { /*2*/ }";
            var src2 = "try { /*1*/ } catch { /*2*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [(E1 e)]@22");
        }

        [Fact]
        public void Catch_DeleteFilter1()
        {
            var src1 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";
            var src2 = "try { /*1*/ } catch (E1 e) { /*2*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [when (e == null)]@29");
        }

        [Fact]
        public void Catch_DeleteFilter2()
        {
            var src1 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";
            var src2 = "try { /*1*/ } catch when (e == null) { /*2*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [(E1 e)]@22");
        }

        [Fact]
        public void Catch_DeleteFilter3()
        {
            var src1 = "try { /*1*/ } catch (E1 e) when (e == null) { /*2*/ }";
            var src2 = "try { /*1*/ } catch { /*2*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [(E1 e)]@22",
                "Delete [when (e == null)]@29");
        }

        [Fact]
        public void TryCatchFinally_DeleteHeader()
        {
            var src1 = "try { /*1*/ } catch { /*2*/ } finally { /*3*/ }";
            var src2 = "{ /*1*/ } { /*2*/ } { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ /*1*/ }]@6 -> @2",
                "Move [{ /*2*/ }]@22 -> @12",
                "Move [{ /*3*/ }]@40 -> @22",
                "Delete [try { /*1*/ } catch { /*2*/ } finally { /*3*/ }]@2",
                "Delete [catch { /*2*/ }]@16",
                "Delete [finally { /*3*/ }]@32");
        }

        [Fact]
        public void TryCatchFinally_InsertHeader()
        {
            var src1 = "{ /*1*/ } { /*2*/ } { /*3*/ }";
            var src2 = "try { /*1*/ } catch { /*2*/ } finally { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [try { /*1*/ } catch { /*2*/ } finally { /*3*/ }]@2",
                "Move [{ /*1*/ }]@2 -> @6",
                "Insert [catch { /*2*/ }]@16",
                "Insert [finally { /*3*/ }]@32",
                "Move [{ /*2*/ }]@12 -> @22",
                "Move [{ /*3*/ }]@22 -> @40");
        }

        [Fact]
        public void TryFilterFinally_InsertHeader()
        {
            var src1 = "{ /*1*/ } if (a == 1) { /*2*/ } { /*3*/ }";
            var src2 = "try { /*1*/ } catch when (a == 1) { /*2*/ } finally { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [try { /*1*/ } catch when (a == 1) { /*2*/ } finally { /*3*/ }]@2",
                "Move [{ /*1*/ }]@2 -> @6",
                "Insert [catch when (a == 1) { /*2*/ }]@16",
                "Insert [finally { /*3*/ }]@46",
                "Insert [when (a == 1)]@22",
                "Move [{ /*2*/ }]@24 -> @36",
                "Move [{ /*3*/ }]@34 -> @54",
                "Delete [if (a == 1) { /*2*/ }]@12");
        }

        #endregion

        #region Blocks

        [Fact]
        public void Block_Insert()
        {
            var src1 = "";
            var src2 = "{ x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [{ x++; }]@2",
                "Insert [x++;]@4");
        }

        [Fact]
        public void Block_Delete()
        {
            var src1 = "{ x++; }";
            var src2 = "";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [{ x++; }]@2",
                "Delete [x++;]@4");
        }

        [Fact]
        public void Block_Reorder()
        {
            var src1 = "{ x++; } { y++; }";
            var src2 = "{ y++; } { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [{ y++; }]@11 -> @2");
        }

        [Fact]
        public void Block_AddLine()
        {
            var src1 = "{ x++; }";
            var src2 = @"{ //
                            x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits();
        }

        #endregion

        #region Checked/Unchecked

        [Fact]
        public void Checked_Insert()
        {
            var src1 = "";
            var src2 = "checked { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [checked { x++; }]@2",
                "Insert [{ x++; }]@10",
                "Insert [x++;]@12");
        }

        [Fact]
        public void Checked_Delete()
        {
            var src1 = "checked { x++; }";
            var src2 = "";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [checked { x++; }]@2",
                "Delete [{ x++; }]@10",
                "Delete [x++;]@12");
        }

        [Fact]
        public void Checked_Update()
        {
            var src1 = "checked { x++; }";
            var src2 = "unchecked { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [checked { x++; }]@2 -> [unchecked { x++; }]@2");
        }

        [Fact]
        public void Checked_DeleteHeader()
        {
            var src1 = "checked { x++; }";
            var src2 = "{ x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ x++; }]@10 -> @2",
                "Delete [checked { x++; }]@2");
        }

        [Fact]
        public void Checked_InsertHeader()
        {
            var src1 = "{ x++; }";
            var src2 = "checked { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [checked { x++; }]@2",
                "Move [{ x++; }]@2 -> @10");
        }

        [Fact]
        public void Unchecked_InsertHeader()
        {
            var src1 = "{ x++; }";
            var src2 = "unchecked { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [unchecked { x++; }]@2",
                "Move [{ x++; }]@2 -> @12");
        }

        #endregion

        #region Unsafe

        [Fact]
        public void Unsafe_Insert()
        {
            var src1 = "";
            var src2 = "unsafe { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [unsafe { x++; }]@2",
                "Insert [{ x++; }]@9",
                "Insert [x++;]@11");
        }

        [Fact]
        public void Unsafe_Delete()
        {
            var src1 = "unsafe { x++; }";
            var src2 = "";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [unsafe { x++; }]@2",
                "Delete [{ x++; }]@9",
                "Delete [x++;]@11");
        }

        [Fact]
        public void Unsafe_DeleteHeader()
        {
            var src1 = "unsafe { x++; }";
            var src2 = "{ x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ x++; }]@9 -> @2",
                "Delete [unsafe { x++; }]@2");
        }

        [Fact]
        public void Unsafe_InsertHeader()
        {
            var src1 = "{ x++; }";
            var src2 = "unsafe { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [unsafe { x++; }]@2",
                "Move [{ x++; }]@2 -> @9");
        }

        #endregion

        #region Using Statement

        [Fact]
        public void Using1()
        {
            string src1 = @"using (a) { using (b) { Foo(); } }";
            string src2 = @"using (a) { using (c) { using (b) { Foo(); } } }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [using (c) { using (b) { Foo(); } }]@14",
                "Insert [{ using (b) { Foo(); } }]@24",
                "Move [using (b) { Foo(); }]@14 -> @26");
        }

        [Fact]
        public void Using_DeleteHeader()
        {
            string src1 = @"using (a) { Foo(); }";
            string src2 = @"{ Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(); }]@12 -> @2",
                "Delete [using (a) { Foo(); }]@2");
        }

        [Fact]
        public void Using_InsertHeader()
        {
            string src1 = @"{ Foo(); }";
            string src2 = @"using (a) { Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [using (a) { Foo(); }]@2",
                "Move [{ Foo(); }]@2 -> @12");
        }

        #endregion

        #region Lock Statement

        [Fact]
        public void Lock1()
        {
            string src1 = @"lock (a) { lock (b) { Foo(); } }";
            string src2 = @"lock (a) { lock (c) { lock (b) { Foo(); } } }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [lock (c) { lock (b) { Foo(); } }]@13",
                "Insert [{ lock (b) { Foo(); } }]@22",
                "Move [lock (b) { Foo(); }]@13 -> @24");
        }

        [Fact]
        public void Lock_DeleteHeader()
        {
            string src1 = @"lock (a) { Foo(); }";
            string src2 = @"{ Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(); }]@11 -> @2",
                "Delete [lock (a) { Foo(); }]@2");
        }

        [Fact]
        public void Lock_InsertHeader()
        {
            string src1 = @"{ Foo(); }";
            string src2 = @"lock (a) { Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [lock (a) { Foo(); }]@2",
                "Move [{ Foo(); }]@2 -> @11");
        }

        #endregion

        #region ForEach Statement

        [Fact]
        public void ForEach1()
        {
            string src1 = @"foreach (var a in e) { foreach (var b in f) { Foo(); } }";
            string src2 = @"foreach (var a in e) { foreach (var c in g) { foreach (var b in f) { Foo(); } } }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [foreach (var c in g) { foreach (var b in f) { Foo(); } }]@25",
                "Insert [{ foreach (var b in f) { Foo(); } }]@46",
                "Move [foreach (var b in f) { Foo(); }]@25 -> @48");

            var actual = ToMatchingPairs(edits.Match);

            var expected = new MatchingPairs
            {
                { "foreach (var a in e) { foreach (var b in f) { Foo(); } }", "foreach (var a in e) { foreach (var c in g) { foreach (var b in f) { Foo(); } } }" },
                { "{ foreach (var b in f) { Foo(); } }", "{ foreach (var c in g) { foreach (var b in f) { Foo(); } } }" },
                { "foreach (var b in f) { Foo(); }", "foreach (var b in f) { Foo(); }" },
                { "{ Foo(); }", "{ Foo(); }" },
                { "Foo();", "Foo();" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void ForEach_Swap1()
        {
            string src1 = @"foreach (var a in e) { foreach (var b in f) { Foo(); } }";
            string src2 = @"foreach (var b in f) { foreach (var a in e) { Foo(); } }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [foreach (var b in f) { Foo(); }]@25 -> @2",
                "Move [foreach (var a in e) { foreach (var b in f) { Foo(); } }]@2 -> @25",
                "Move [Foo();]@48 -> @48");

            var actual = ToMatchingPairs(edits.Match);

            var expected = new MatchingPairs
            {
                { "foreach (var a in e) { foreach (var b in f) { Foo(); } }", "foreach (var a in e) { Foo(); }" },
                { "{ foreach (var b in f) { Foo(); } }", "{ Foo(); }" },
                { "foreach (var b in f) { Foo(); }", "foreach (var b in f) { foreach (var a in e) { Foo(); } }" },
                { "{ Foo(); }", "{ foreach (var a in e) { Foo(); } }" },
                { "Foo();", "Foo();" }
            };

            expected.AssertEqual(actual);
        }

        [Fact]
        public void Foreach_DeleteHeader()
        {
            string src1 = @"foreach (var a in b) { Foo(); }";
            string src2 = @"{ Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(); }]@23 -> @2",
                "Delete [foreach (var a in b) { Foo(); }]@2");
        }

        [Fact]
        public void Foreach_InsertHeader()
        {
            string src1 = @"{ Foo(); }";
            string src2 = @"foreach (var a in b) { Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [foreach (var a in b) { Foo(); }]@2",
                "Move [{ Foo(); }]@2 -> @23");
        }

        [Fact]
        public void ForeachVariable_Update1()
        {
            var src1 = @"
foreach (var (a1, a2) in e) { }
foreach ((var b1, var b2) in e) { }
foreach (var a in e1) { yield return 7; }
";

            var src2 = @"
foreach (var (a1, a3) in e) { }
foreach ((var b3, int b2) in e) { }
foreach (_ in e1) { yield return 7; }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [foreach ((var b1, var b2) in e) { }]@37 -> [foreach ((var b3, int b2) in e) { }]@37",
                "Update [foreach (var a in e1) { yield return 7; }]@74 -> [foreach (_ in e1) { yield return 7; }]@74",
                "Update [a2]@22 -> [a3]@22",
                "Update [b1]@51 -> [b3]@51");
        }

        [Fact]
        public void ForeachVariable_Update2()
        {
            var src1 = @"
foreach (_ in e2) { yield return 5; }
foreach (_ in e3) {  A(); }
";

            var src2 = @"
foreach (var b in e2) { yield return 5; }
foreach (_ in e4) { A(); }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [foreach (_ in e2) { yield return 5; }]@4 -> [foreach (var b in e2) { yield return 5; }]@4",
                "Update [foreach (_ in e3) {  A(); }]@43 -> [foreach (_ in e4) { A(); }]@47");
        }

        [Fact]
        public void ForeachVariable_Insert()
        {
            var src1 = @"
foreach (var (a3, a4) in e) { }
foreach ((var b4, var b5) in e) { }
";

            var src2 = @"
foreach (var (a3, a5, a4) in e) { }
foreach ((var b6, var b4, var b5) in e) { }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [foreach (var (a3, a4) in e) { }]@4 -> [foreach (var (a3, a5, a4) in e) { }]@4",
                "Update [foreach ((var b4, var b5) in e) { }]@37 -> [foreach ((var b6, var b4, var b5) in e) { }]@41",
                "Insert [a5]@22",
                "Insert [b6]@55");
        }

        [Fact]
        public void ForeachVariable_Delete()
        {
            var src1 = @"
foreach (var (a11, a12, a13) in e) { F(); }
foreach ((var b7, var b8, var b9) in e) { G(); }
";

            var src2 = @"
foreach (var (a12, a13) in e1) { F(); }
foreach ((var b7, var b9) in e) { G(); }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [foreach (var (a11, a12, a13) in e) { F(); }]@4 -> [foreach (var (a12, a13) in e1) { F(); }]@4",
                "Update [foreach ((var b7, var b8, var b9) in e) { G(); }]@49 -> [foreach ((var b7, var b9) in e) { G(); }]@45",
                "Delete [a11]@18",
                "Delete [b8]@71");
        }

        [Fact]
        public void ForeachVariable_Reorder()
        {
            var src1 = @"
foreach (var (a, b) in e1) { }
foreach ((var x, var y) in e2) { }
";

            var src2 = @"
foreach ((var x, var y) in e2) { }
foreach (var (a, b) in e1) { }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [foreach ((var x, var y) in e2) { }]@36 -> @4");
        }

        [Fact]
        public void ForeachVariableEmbedded_Reorder()
        {
            var src1 = @"
foreach (var (a, b) in e1) { 
    foreach ((var x, var y) in e2) { }
}
";

            var src2 = @"
foreach ((var x, var y) in e2) { }
foreach (var (a, b) in e1) { }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [foreach ((var x, var y) in e2) { }]@39 -> @4");
        }


        #endregion

        #region For Statement

        [Fact]
        public void For1()
        {
            string src1 = @"for (int a = 0; a < 10; a++) { for (int a = 0; a < 20; a++) { Foo(); } }";
            string src2 = @"for (int a = 0; a < 10; a++) { for (int b = 0; b < 10; b++) { for (int a = 0; a < 20; a++) { Foo(); } } }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [for (int b = 0; b < 10; b++) { for (int a = 0; a < 20; a++) { Foo(); } }]@33",
                "Insert [int b = 0]@38",
                "Insert [b < 10]@49",
                "Insert [b++]@57",
                "Insert [{ for (int a = 0; a < 20; a++) { Foo(); } }]@62",
                "Insert [b = 0]@42",
                "Move [for (int a = 0; a < 20; a++) { Foo(); }]@33 -> @64");
        }

        [Fact]
        public void For_DeleteHeader()
        {
            string src1 = @"for (int i = 10, j = 0; i > j; i--, j++) { Foo(); }";
            string src2 = @"{ Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(); }]@43 -> @2",
                "Delete [for (int i = 10, j = 0; i > j; i--, j++) { Foo(); }]@2",
                "Delete [int i = 10, j = 0]@7",
                "Delete [i = 10]@11",
                "Delete [j = 0]@19",
                "Delete [i > j]@26",
                "Delete [i--]@33",
                "Delete [j++]@38");
        }

        [Fact]
        public void For_InsertHeader()
        {
            string src1 = @"{ Foo(); }";
            string src2 = @"for (int i = 10, j = 0; i > j; i--, j++) { Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [for (int i = 10, j = 0; i > j; i--, j++) { Foo(); }]@2",
                "Insert [int i = 10, j = 0]@7",
                "Insert [i > j]@26",
                "Insert [i--]@33",
                "Insert [j++]@38",
                "Move [{ Foo(); }]@2 -> @43",
                "Insert [i = 10]@11",
                "Insert [j = 0]@19");
        }

        [Fact]
        public void For_DeclaratorsToInitializers()
        {
            string src1 = @"for (var i = 10; i < 10; i++) { }";
            string src2 = @"for (i = 10; i < 10; i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [i = 10]@7",
                "Delete [var i = 10]@7",
                "Delete [i = 10]@11");
        }

        [Fact]
        public void For_InitializersToDeclarators()
        {
            string src1 = @"for (i = 10, j = 0; i < 10; i++) { }";
            string src2 = @"for (var i = 10, j = 0; i < 10; i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [var i = 10, j = 0]@7",
                "Insert [i = 10]@11",
                "Insert [j = 0]@19",
                "Delete [i = 10]@7",
                "Delete [j = 0]@15");
        }

        [Fact]
        public void For_Declarations_Reorder()
        {
            string src1 = @"for (var i = 10, j = 0; i > j; i++, j++) { }";
            string src2 = @"for (var j = 0, i = 10; i > j; i++, j++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Reorder [j = 0]@19 -> @11");
        }

        [Fact]
        public void For_Declarations_Insert()
        {
            string src1 = @"for (var i = 0, j = 1; i > j; i++, j++) { }";
            string src2 = @"for (var i = 0, j = 1, k = 2; i > j; i++, j++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [var i = 0, j = 1]@7 -> [var i = 0, j = 1, k = 2]@7",
                "Insert [k = 2]@25");
        }

        [Fact]
        public void For_Declarations_Delete()
        {
            string src1 = @"for (var i = 0, j = 1, k = 2; i > j; i++, j++) { }";
            string src2 = @"for (var i = 0, j = 1; i > j; i++, j++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [var i = 0, j = 1, k = 2]@7 -> [var i = 0, j = 1]@7",
                "Delete [k = 2]@25");
        }

        [Fact]
        public void For_Initializers_Reorder()
        {
            string src1 = @"for (i = 10, j = 0; i > j; i++, j++) { }";
            string src2 = @"for (j = 0, i = 10; i > j; i++, j++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Reorder [j = 0]@15 -> @7");
        }

        [Fact]
        public void For_Initializers_Insert()
        {
            string src1 = @"for (i = 10; i < 10; i++) { }";
            string src2 = @"for (i = 10, j = 0; i < 10; i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Insert [j = 0]@15");
        }

        [Fact]
        public void For_Initializers_Delete()
        {
            string src1 = @"for (i = 10, j = 0; i < 10; i++) { }";
            string src2 = @"for (i = 10; i < 10; i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Delete [j = 0]@15");
        }

        [Fact]
        public void For_Initializers_Update()
        {
            string src1 = @"for (i = 1; i < 10; i++) { }";
            string src2 = @"for (i = 2; i < 10; i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [i = 1]@7 -> [i = 2]@7");
        }

        [Fact]
        public void For_Initializers_Update_Lambda()
        {
            string src1 = @"for (int i = 10, j = F(() => 1); i > j; i++) { }";
            string src2 = @"for (int i = 10, j = F(() => 2); i > j; i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [() => 1]@25 -> [() => 2]@25");
        }

        [Fact]
        public void For_Condition_Update()
        {
            string src1 = @"for (int i = 0; i < 10; i++) { }";
            string src2 = @"for (int i = 0; i < 20; i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [i < 10]@18 -> [i < 20]@18");
        }

        [Fact]
        public void For_Condition_Lambda()
        {
            string src1 = @"for (int i = 0; F(() => 1); i++) { }";
            string src2 = @"for (int i = 0; F(() => 2); i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [() => 1]@20 -> [() => 2]@20");
        }

        [Fact]
        public void For_Incrementors_Reorder()
        {
            string src1 = @"for (int i = 10, j = 0; i > j; i--, j++) { }";
            string src2 = @"for (int i = 10, j = 0; i > j; j++, i--) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Reorder [j++]@38 -> @33");
        }

        [Fact]
        public void For_Incrementors_Insert()
        {
            string src1 = @"for (int i = 10, j = 0; i > j; i--) { }";
            string src2 = @"for (int i = 10, j = 0; i > j; j++, i--) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Insert [j++]@33");
        }

        [Fact]
        public void For_Incrementors_Delete()
        {
            string src1 = @"for (int i = 10, j = 0; i > j; j++, i--) { }";
            string src2 = @"for (int i = 10, j = 0; i > j; j++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Delete [i--]@38");
        }

        [Fact]
        public void For_Incrementors_Update()
        {
            string src1 = @"for (int i = 10, j = 0; i > j; j++) { }";
            string src2 = @"for (int i = 10, j = 0; i > j; i++) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [j++]@33 -> [i++]@33");
        }

        [Fact]
        public void For_Incrementors_Update_Lambda()
        {
            string src1 = @"for (int i = 10, j = 0; i > j; F(() => 1)) { }";
            string src2 = @"for (int i = 10, j = 0; i > j; F(() => 2)) { }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits("Update [() => 1]@35 -> [() => 2]@35");
        }

        #endregion

        #region While Statement

        [Fact]
        public void While1()
        {
            string src1 = @"while (a) { while (b) { Foo(); } }";
            string src2 = @"while (a) { while (c) { while (b) { Foo(); } } }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [while (c) { while (b) { Foo(); } }]@14",
                "Insert [{ while (b) { Foo(); } }]@24",
                "Move [while (b) { Foo(); }]@14 -> @26");
        }

        [Fact]
        public void While_DeleteHeader()
        {
            string src1 = @"while (a) { Foo(); }";
            string src2 = @"{ Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(); }]@12 -> @2",
                "Delete [while (a) { Foo(); }]@2");
        }

        [Fact]
        public void While_InsertHeader()
        {
            string src1 = @"{ Foo(); }";
            string src2 = @"while (a) { Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [while (a) { Foo(); }]@2",
                "Move [{ Foo(); }]@2 -> @12");
        }

        #endregion

        #region Do Statement

        [Fact]
        public void Do1()
        {
            string src1 = @"do { do { Foo(); } while (b); } while (a);";
            string src2 = @"do { do { do { Foo(); } while(b); } while(c); } while(a);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [do { do { Foo(); } while(b); } while(c);]@7",
                "Insert [{ do { Foo(); } while(b); }]@10",
                "Move [do { Foo(); } while (b);]@7 -> @12");
        }

        [Fact]
        public void Do_DeleteHeader()
        {
            string src1 = @"do { Foo(); } while (a);";
            string src2 = @"{ Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(); }]@5 -> @2",
                "Delete [do { Foo(); } while (a);]@2");
        }

        [Fact]
        public void Do_InsertHeader()
        {
            string src1 = @"{ Foo(); }";
            string src2 = @"do { Foo(); } while (a);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [do { Foo(); } while (a);]@2",
                "Move [{ Foo(); }]@2 -> @5");
        }

        #endregion

        #region If Statement

        [Fact]
        public void IfStatement_TestExpression_Update()
        {
            var src1 = "var x = 1; if (x == 1) { x++; }";
            var src2 = "var x = 1; if (x == 2) { x++; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [if (x == 1) { x++; }]@13 -> [if (x == 2) { x++; }]@13");
        }

        [Fact]
        public void ElseClause_Insert()
        {
            var src1 = "if (x == 1) x++; ";
            var src2 = "if (x == 1) x++; else y++;";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [else y++;]@19",
                "Insert [y++;]@24");
        }

        [Fact]
        public void ElseClause_InsertMove()
        {
            var src1 = "if (x == 1) x++; else y++;";
            var src2 = "if (x == 1) x++; else if (x == 2) y++;";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [if (x == 2) y++;]@24",
                "Move [y++;]@24 -> @36");
        }

        [Fact]
        public void If1()
        {
            string src1 = @"if (a) if (b) Foo();";
            string src2 = @"if (a) if (c) if (b) Foo();";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [if (c) if (b) Foo();]@9",
                "Move [if (b) Foo();]@9 -> @16");
        }

        [Fact]
        public void If_DeleteHeader()
        {
            string src1 = @"if (a) { Foo(); }";
            string src2 = @"{ Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(); }]@9 -> @2",
                "Delete [if (a) { Foo(); }]@2");
        }

        [Fact]
        public void If_InsertHeader()
        {
            string src1 = @"{ Foo(); }";
            string src2 = @"if (a) { Foo(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [if (a) { Foo(); }]@2",
                "Move [{ Foo(); }]@2 -> @9");
        }

        [Fact]
        public void Else_DeleteHeader()
        {
            string src1 = @"if (a) { Foo(/*1*/); } else { Foo(/*2*/); }";
            string src2 = @"if (a) { Foo(/*1*/); } { Foo(/*2*/); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(/*2*/); }]@30 -> @25",
                "Delete [else { Foo(/*2*/); }]@25");
        }

        [Fact]
        public void Else_InsertHeader()
        {
            string src1 = @"if (a) { Foo(/*1*/); } { Foo(/*2*/); }";
            string src2 = @"if (a) { Foo(/*1*/); } else { Foo(/*2*/); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [else { Foo(/*2*/); }]@25",
                "Move [{ Foo(/*2*/); }]@25 -> @30");
        }

        [Fact]
        public void ElseIf_DeleteHeader()
        {
            string src1 = @"if (a) { Foo(/*1*/); } else if (b) { Foo(/*2*/); }";
            string src2 = @"if (a) { Foo(/*1*/); } { Foo(/*2*/); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ Foo(/*2*/); }]@37 -> @25",
                "Delete [else if (b) { Foo(/*2*/); }]@25",
                "Delete [if (b) { Foo(/*2*/); }]@30");
        }

        [Fact]
        public void ElseIf_InsertHeader()
        {
            string src1 = @"if (a) { Foo(/*1*/); } { Foo(/*2*/); }";
            string src2 = @"if (a) { Foo(/*1*/); } else if (b) { Foo(/*2*/); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [else if (b) { Foo(/*2*/); }]@25",
                "Insert [if (b) { Foo(/*2*/); }]@30",
                "Move [{ Foo(/*2*/); }]@25 -> @37");
        }

        [Fact]
        public void IfElseElseIf_InsertHeader()
        {
            string src1 = @"{ /*1*/ } { /*2*/ } { /*3*/ }";
            string src2 = @"if (a) { /*1*/ } else if (b) { /*2*/ } else { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [if (a) { /*1*/ } else if (b) { /*2*/ } else { /*3*/ }]@2",
                "Move [{ /*1*/ }]@2 -> @9",
                "Insert [else if (b) { /*2*/ } else { /*3*/ }]@19",
                "Insert [if (b) { /*2*/ } else { /*3*/ }]@24",
                "Move [{ /*2*/ }]@12 -> @31",
                "Insert [else { /*3*/ }]@41",
                "Move [{ /*3*/ }]@22 -> @46");
        }

        [Fact]
        public void IfElseElseIf_DeleteHeader()
        {
            string src1 = @"if (a) { /*1*/ } else if (b) { /*2*/ } else { /*3*/ }";
            string src2 = @"{ /*1*/ } { /*2*/ } { /*3*/ }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Move [{ /*1*/ }]@9 -> @2",
                "Move [{ /*2*/ }]@31 -> @12",
                "Move [{ /*3*/ }]@46 -> @22",
                "Delete [if (a) { /*1*/ } else if (b) { /*2*/ } else { /*3*/ }]@2",
                "Delete [else if (b) { /*2*/ } else { /*3*/ }]@19",
                "Delete [if (b) { /*2*/ } else { /*3*/ }]@24",
                "Delete [else { /*3*/ }]@41");
        }

        #endregion

        #region Switch Statement

        [Fact]
        public void SwitchStatement_Update_Expression()
        {
            var src1 = "var x = 1; switch (x + 1) { case 1: break; }";
            var src2 = "var x = 1; switch (x + 2) { case 1: break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [switch (x + 1) { case 1: break; }]@13 -> [switch (x + 2) { case 1: break; }]@13");
        }

        [Fact]
        public void SwitchStatement_Update_SectionLabel()
        {
            var src1 = "var x = 1; switch (x) { case 1: break; }";
            var src2 = "var x = 1; switch (x) { case 2: break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [case 1: break;]@26 -> [case 2: break;]@26");
        }

        [Fact]
        public void SwitchStatement_Update_AddSectionLabel()
        {
            var src1 = "var x = 1; switch (x) { case 1: break; }";
            var src2 = "var x = 1; switch (x) { case 1: case 2: break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [case 1: break;]@26 -> [case 1: case 2: break;]@26");
        }

        [Fact]
        public void SwitchStatement_Update_DeleteSectionLabel()
        {
            var src1 = "var x = 1; switch (x) { case 1: case 2: break; }";
            var src2 = "var x = 1; switch (x) { case 1: break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [case 1: case 2: break;]@26 -> [case 1: break;]@26");
        }

        [Fact]
        public void SwitchStatement_Update_BlockInSection()
        {
            var src1 = "var x = 1; switch (x) { case 1: { x++; break; } }";
            var src2 = "var x = 1; switch (x) { case 1: { x--; break; } }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [x++;]@36 -> [x--;]@36");
        }

        [Fact]
        public void SwitchStatement_Update_BlockInDefaultSection()
        {
            var src1 = "var x = 1; switch (x) { default: { x++; break; } }";
            var src2 = "var x = 1; switch (x) { default: { x--; break; } }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [x++;]@37 -> [x--;]@37");
        }

        [Fact]
        public void SwitchStatement_Insert_Section()
        {
            var src1 = "var x = 1; switch (x) { case 1: break; }";
            var src2 = "var x = 1; switch (x) { case 1: break; case 2: break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [case 2: break;]@41",
                "Insert [break;]@49");
        }

        [Fact]
        public void SwitchStatement_Delete_Section()
        {
            var src1 = "var x = 1; switch (x) { case 1: break; case 2: break; }";
            var src2 = "var x = 1; switch (x) { case 1: break; }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Delete [case 2: break;]@41",
                "Delete [break;]@49");
        }

        #endregion

        #region Lambdas

        [Fact]
        public void Lambdas_InVariableDeclarator()
        {
            var src1 = "Action x = a => a, y = b => b;";
            var src2 = "Action x = (a) => a, y = b => b + 1;";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [a => a]@13 -> [(a) => a]@13",
                "Update [b => b]@25 -> [b => b + 1]@27");
        }

        [Fact]
        public void Lambdas_InExpressionStatement()
        {
            var src1 = "F(a => a, b => b);";
            var src2 = "F(b => b, a => a+1);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [b => b]@12 -> @4",
                "Update [a => a]@4 -> [a => a+1]@12");
        }

        [Fact]
        public void Lambdas_ReorderArguments()
        {
            var src1 = "F(G(a => {}), G(b => {}));";
            var src2 = "F(G(b => {}), G(a => {}));";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [b => {}]@18 -> @6");
        }

        [Fact]
        public void Lambdas_InWhile()
        {
            var src1 = "while (F(a => a)) { /*1*/ }";
            var src2 = "do { /*1*/ } while (F(a => a));";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Insert [do { /*1*/ } while (F(a => a));]@2",
                "Move [{ /*1*/ }]@20 -> @5",
                "Move [a => a]@11 -> @24",
                "Delete [while (F(a => a)) { /*1*/ }]@2");
        }

        [Fact]
        public void Lambdas_InLambda()
        {
            var src1 = "F(() => { G(x => y); });";
            var src2 = "F(q => { G(() => y); });";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [() => { G(x => y); }]@4 -> [q => { G(() => y); }]@4");
        }

        [Fact]
        public void Lambdas_Insert_Static_Top()
        {
            var src1 = @"
using System;

class C
{
    void F()
    {
    }
}
";
            var src2 = @"
using System;

class C
{
    void F()
    {
        var f = new Func<int, int>(a => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Insert_Static_Nested1()
        {
            var src1 = @"
using System;

class C
{
    static int G(Func<int, int> f) => 0;

    void F()
    {
        G(a => a);
    }
}
";
            var src2 = @"
using System;

class C
{
    static int G(Func<int, int> f) => 0;
   
    void F()
    {
        G(a => G(b => b) + a);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Insert_ThisOnly_Top1()
        {
            var src1 = @"
using System;

class C
{
    int x = 0;
    int G(Func<int, int> f) => 0;

    void F()
    {
        
    }
}
";
            var src2 = @"
using System;

class C
{
    int x = 0;
    int G(Func<int, int> f) => 0;
   
    void F()
    {
        G(a => x);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            // TODO: allow creating a new leaf closure
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "F", "this"));
        }

        [Fact, WorkItem(1291, "https://github.com/dotnet/roslyn/issues/1291")]
        public void Lambdas_Insert_ThisOnly_Top2()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        int y = 1;
        {
            int x = 2;
            var f1 = new Func<int, int>(a => y);
        }
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        int y = 1;
        {
            int x = 2;
            var f2 = from a in new[] { 1 } select a + y;
            var f3 = from a in new[] { 1 } where x > 0 select a;
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "x", "x"));
        }

        [Fact]
        public void Lambdas_Insert_ThisOnly_Nested1()
        {
            var src1 = @"
using System;

class C
{
    int x = 0;
    int G(Func<int, int> f) => 0;

    void F()
    {
        G(a => a);
    }
}
";
            var src2 = @"
using System;

class C
{
    int x = 0;
    int G(Func<int, int> f) => 0;
   
    void F()
    {
        G(a => G(b => x));
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "F", "this"));
        }

        [Fact]
        public void Lambdas_Insert_ThisOnly_Nested2()
        {
            var src1 = @"
using System;

class C
{
    int x = 0;

    void F()
    {
        var f1 = new Func<int, int>(a => 
        {
            var f2 = new Func<int, int>(b => 
            {
                return b;
            });

            return a;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    int x = 0;
   
    void F()
    {
        var f1 = new Func<int, int>(a => 
        {
            var f2 = new Func<int, int>(b => 
            {
                return b;
            });

            var f3 = new Func<int, int>(c => 
            {
                return c + x;
            });

            return a;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "F", "this"));
        }

        [Fact]
        public void Lambdas_InsertAndDelete_Scopes1()
        {
            var src1 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    int x = 0, y = 0;                      // Group #0

    void F()
    {
        int x0 = 0, y0 = 0;                // Group #1 
                                         
        { int x1 = 0, y1 = 0;              // Group #2 
                                           
            { int x2 = 0, y2 = 0;          // Group #1 
                                            
                { int x3 = 0, y3 = 0;      // Group #2 
                                           
                    G(a => x3 + x1);       
                    G(a => x0 + y0 + x2);
                    G(a => x);
                }
            }
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    int x = 0, y = 0;                       // Group #0

    void F()
    {
        int x0 = 0, y0 = 0;                 // Group #1
                                           
        { int x1 = 0, y1 = 0;               // Group #2 
                                           
            { int x2 = 0, y2 = 0;           // Group #1
                                           
                { int x3 = 0, y3 = 0;       // Group #2 
                                            
                    G(a => x3 + x1);        
                    G(a => x0 + y0 + x2);
                    G(a => x);

                    G(a => x);              // OK
                    G(a => x0 + y0);        // OK
                    G(a => x1 + y0);        // error - connecting Group #1 and Group #2
                    G(a => x3 + x1);        // error - multi-scope (conservative)
                    G(a => x + y0);         // error - connecting Group #0 and Group #1
                    G(a => x + x3);         // error - connecting Group #0 and Group #2
                }
            }
        }
    }
}
";
            var insert = GetTopEdits(src1, src2);

            insert.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x1", CSharpFeaturesResources.lambda, "y0", "x1"),
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x3", CSharpFeaturesResources.lambda, "x1", "x3"),
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "y0", CSharpFeaturesResources.lambda, "this", "y0"),
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x3", CSharpFeaturesResources.lambda, "this", "x3"));

            var delete = GetTopEdits(src2, src1);

            delete.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.DeleteLambdaWithMultiScopeCapture, "x1", CSharpFeaturesResources.lambda, "y0", "x1"),
                Diagnostic(RudeEditKind.DeleteLambdaWithMultiScopeCapture, "x3", CSharpFeaturesResources.lambda, "x1", "x3"),
                Diagnostic(RudeEditKind.DeleteLambdaWithMultiScopeCapture, "y0", CSharpFeaturesResources.lambda, "this", "y0"),
                Diagnostic(RudeEditKind.DeleteLambdaWithMultiScopeCapture, "x3", CSharpFeaturesResources.lambda, "this", "x3"));
        }

        [Fact]
        public void Lambdas_Insert_ForEach1()
        {
            var src1 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    void F()                       
    {                              
        foreach (int x0 in new[] { 1 })  // Group #0             
        {                                // Group #1
            int x1 = 0;                  
                                         
            G(a => x0);   
            G(a => x1);
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    void F()                       
    {                              
        foreach (int x0 in new[] { 1 })  // Group #0             
        {                                // Group #1
            int x1 = 0;                  
                                         
            G(a => x0);   
            G(a => x1);

            G(a => x0 + x1);             // error: connecting previously disconnected closures
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x1", CSharpFeaturesResources.lambda, "x0", "x1"));
        }

        [Fact]
        public void Lambdas_Insert_ForEach2()
        {
            var src1 = @"
using System;

class C
{
    void G(Func<int, int> f1, Func<int, int> f2, Func<int, int> f3) {}

    void F()                       
    {               
        int x0 = 0;                              // Group #0  
        foreach (int x1 in new[] { 1 })          // Group #1                   
            G(a => x0, a => x1, null);                     
    }
}
";
            var src2 = @"
using System;

class C
{
    void G(Func<int, int> f1, Func<int, int> f2, Func<int, int> f3) {}

    void F()                       
    {               
        int x0 = 0;                              // Group #0  
        foreach (int x1 in new[] { 1 })          // Group #1            
            G(a => x0, a => x1, a => x0 + x1);   // error: connecting previously disconnected closures            
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x1", CSharpFeaturesResources.lambda, "x0", "x1"));
        }

        [Fact]
        public void Lambdas_Insert_For1()
        {
            var src1 = @"
using System;

class C
{
    bool G(Func<int, int> f) => true;

    void F()                       
    {                              
        for (int x0 = 0, x1 = 0; G(a => x0) && G(a => x1);)
        {
            int x2 = 0;
            G(a => x2); 
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    bool G(Func<int, int> f) => true;

    void F()                       
    {                              
        for (int x0 = 0, x1 = 0; G(a => x0) && G(a => x1);)
        {
            int x2 = 0;
            G(a => x2); 

            G(a => x0 + x1);  // ok
            G(a => x0 + x2);  // error: connecting previously disconnected closures
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x2", CSharpFeaturesResources.lambda, "x0", "x2"));
        }

        [Fact]
        public void Lambdas_Insert_Switch1()
        {
            var src1 = @"
using System;

class C
{
    bool G(Func<int> f) => true;

    int a = 1;

    void F()                       
    {        
        int x2 = 1;
        G(() => x2);
                      
        switch (a)
        {
            case 1:
                int x0 = 1;
                G(() => x0);
                break;

            case 2:
                int x1 = 1;
                G(() => x1);
                break;
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    bool G(Func<int> f) => true;

    int a = 1;

    void F()                       
    {                
        int x2 = 1;
        G(() => x2);
 
        switch (a)
        {
            case 1:
                int x0 = 1;
                G(() => x0);
                goto case 2;

            case 2:
                int x1 = 1;
                G(() => x1);
                goto default;

            default:
                x0 = 1;
                x1 = 2;
                G(() => x0 + x1);       // ok
                G(() => x0 + x2);       // error
                break;
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x0", CSharpFeaturesResources.lambda, "x2", "x0"));
        }

        [Fact]
        public void Lambdas_Insert_Using1()
        {
            var src1 = @"
using System;

class C
{
    static bool G<T>(Func<T> f) => true;
    static int F(object a, object b) => 1;

    static IDisposable D() => null;
    
    static void F()                       
    {                              
        using (IDisposable x0 = D(), y0 = D())
        {
            int x1 = 1;
        
            G(() => x0);
            G(() => y0);
            G(() => x1);
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    static bool G<T>(Func<T> f) => true;
    static int F(object a, object b) => 1;

    static IDisposable D() => null;
    
    static void F()                       
    {                              
        using (IDisposable x0 = D(), y0 = D())
        {
            int x1 = 1;
        
            G(() => x0);
            G(() => y0);
            G(() => x1);

            G(() => F(x0, y0)); // ok
            G(() => F(x0, x1)); // error
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x1", CSharpFeaturesResources.lambda, "x0", "x1"));
        }

        [Fact]
        public void Lambdas_Insert_Catch1()
        {
            var src1 = @"
using System;

class C
{
    static bool G<T>(Func<T> f) => true;
    static int F(object a, object b) => 1;
    
    static void F()                       
    {                              
        try
        {
        }
        catch (Exception x0)
        {
            int x1 = 1;
            G(() => x0);
            G(() => x1);
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    static bool G<T>(Func<T> f) => true;
    static int F(object a, object b) => 1;
    
    static void F()                       
    {                              
        try
        {
        }
        catch (Exception x0)
        {
            int x1 = 1;
            G(() => x0);
            G(() => x1);

            G(() => x0); //ok
            G(() => F(x0, x1)); //error
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x1", CSharpFeaturesResources.lambda, "x0", "x1"));
        }

        [Fact, WorkItem(1504, "https://github.com/dotnet/roslyn/issues/1504")]
        public void Lambdas_Insert_CatchFilter1()
        {
            var src1 = @"
using System;

class C
{
    static bool G<T>(Func<T> f) => true;
    
    static void F()                       
    {                              
        Exception x1 = null;
    
        try
        {
            G(() => x1);
        }
        catch (Exception x0) when (G(() => x0))
        {
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    static bool G<T>(Func<T> f) => true;
    
    static void F()                       
    {                 
        Exception x1 = null;
             
        try
        {
            G(() => x1);
        }
        catch (Exception x0) when (G(() => x0) && 
                                   G(() => x0) &&    // ok
                                   G(() => x0 != x1)) // error
        {
            G(() => x0); // ok
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x0", CSharpFeaturesResources.lambda, "x1", "x0")
                );
        }


        [Fact]
        public void Lambdas_Update_CeaseCapture_This()
        {
            var src1 = @"
using System;

class C
{
    int x = 1;

    void F()
    {
        var f = new Func<int, int>(a => a + x);
    }
}
";
            var src2 = @"
using System;

class C
{
    int x;
   
    void F()
    {
        var f = new Func<int, int>(a => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "F", "this"));
        }

        [Fact]
        public void Lambdas_Update_Signature1()
        {
            var src1 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<long, long> f) {}

    void F()
    {
        G1(a => a);
    }
}
";
            var src2 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<long, long> f) {}

    void F()
    {
        G2(a => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaParameters, "a", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Signature2()
        {
            var src1 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, int, int> f) {}

    void F()
    {
        G1(a => a);
    }
}
";
            var src2 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, int, int> f) {}

    void F()
    {
        G2((a, b) => a + b);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaParameters, "(a, b)", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Signature3()
        {
            var src1 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, long> f) {}

    void F()
    {
        G1(a => a);
    }
}
";
            var src2 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, long> f) {}

    void F()
    {
        G2(a => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaReturnType, "a", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Signature_SyntaxOnly1()
        {
            var src1 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, int> f) {}

    void F()
    {
        G1(a => a);
    }
}
";
            var src2 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, int> f) {}

    void F()
    {
        G2((a) => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_Signature_ReturnType1()
        {
            var src1 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Action<int> f) {}

    void F()
    {
        G1(a => { return 1; });
    }
}
";
            var src2 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Action<int> f) {}

    void F()
    {
        G2(a => { });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaReturnType, "a", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Signature_BodySyntaxOnly()
        {
            var src1 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, int> f) {}

    void F()
    {
        G1(a => { return 1; });
    }
}
";
            var src2 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, int> f) {}

    void F()
    {
        G2(a => 2);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_Signature_ParameterName1()
        {
            var src1 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, int> f) {}

    void F()
    {
        G1(a => 1);
    }
}
";
            var src2 = @"
using System;

class C
{
    void G1(Func<int, int> f) {}
    void G2(Func<int, int> f) {}

    void F()
    {
        G2(b => 2);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_Signature_ParameterRefness1()
        {
            var src1 = @"
using System;

delegate int D1(ref int a);
delegate int D2(int a);

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G1((ref int a) => 1);
    }
}
";
            var src2 = @"
using System;

delegate int D1(ref int a);
delegate int D2(int a);

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G2((int a) => 2);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaParameters, "(int a)", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Signature_ParameterRefness2()
        {
            var src1 = @"
using System;

delegate int D1(ref int a);
delegate int D2(out int a);

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G1((ref int a) => a = 1);
    }
}
";
            var src2 = @"
using System;

delegate int D1(ref int a);
delegate int D2(out int a);

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G2((out int a) => a = 1);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaParameters, "(out int a)", CSharpFeaturesResources.lambda));
        }

        // Add corresponding test to VB
        [WpfFact(Skip = "TODO")]
        public void Lambdas_Update_Signature_CustomModifiers1()
        {
            var delegateSource = @"
.class public auto ansi sealed D1
       extends [mscorlib]System.MulticastDelegate
{
  .method public specialname rtspecialname instance void .ctor(object 'object', native int 'method') runtime managed
  {
  }

  .method public newslot virtual instance int32 [] modopt([mscorlib]System.Int64) Invoke(
      int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) a, 
      int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) b) runtime managed
  {
  }
}

.class public auto ansi sealed D2
       extends [mscorlib]System.MulticastDelegate
{
  .method public specialname rtspecialname instance void .ctor(object 'object', native int 'method') runtime managed
  {
  }

  .method public newslot virtual instance int32 [] modopt([mscorlib]System.Boolean) Invoke(
      int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) a, 
      int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) b) runtime managed
  {
  }
}

.class public auto ansi sealed D3
       extends [mscorlib]System.MulticastDelegate
{
  .method public specialname rtspecialname instance void .ctor(object 'object', native int 'method') runtime managed
  {
  }

  .method public newslot virtual instance int32 [] modopt([mscorlib]System.Boolean) Invoke(
      int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) a, 
      int32 modopt([mscorlib]System.Runtime.CompilerServices.IsConst) b) runtime managed
  {
  }
}";

            var src1 = @"
using System;

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G1(a => a);
    }
}
";
            var src2 = @"
using System;

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G2(a => a);
    }
}
";
            MetadataReference delegateDefs;
            using (var tempAssembly = IlasmUtilities.CreateTempAssembly(delegateSource))
            {
                delegateDefs = MetadataReference.CreateFromImage(File.ReadAllBytes(tempAssembly.Path));
            }

            var edits = GetTopEdits(src1, src2);

            // TODO
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_DelegateType1()
        {
            var src1 = @"
using System;

delegate int D1(int a);
delegate int D2(int a);

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G1(a => a);
    }
}
";
            var src2 = @"
using System;

delegate int D1(int a);
delegate int D2(int a);

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G2(a => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_SourceType1()
        {
            var src1 = @"
using System;

delegate C D1(C a);
delegate C D2(C a);

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G1(a => a);
    }
}
";
            var src2 = @"
using System;

delegate C D1(C a);
delegate C D2(C a);

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G2(a => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_SourceType2()
        {
            var src1 = @"
using System;

delegate C D1(C a);
delegate B D2(B a);

class B { }

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G1(a => a);
    }
}
";
            var src2 = @"
using System;

delegate C D1(C a);
delegate B D2(B a);

class B { }

class C
{
    void G1(D1 f) {}
    void G2(D2 f) {}

    void F()
    {
        G2(a => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaParameters, "a", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_SourceTypeAndMetadataType1()
        {
            var src1 = @"
namespace System
{
    delegate string D1(string a);
    delegate String D2(String a);

    class String { }

    class C
    {
        void G1(D1 f) {}
        void G2(D2 f) {}

        void F()
        {
            G1(a => a);
        }
    }
}
";
            var src2 = @"
namespace System
{
    delegate string D1(string a);
    delegate String D2(String a);

    class String { }

    class C
    {
        void G1(D1 f) {}
        void G2(D2 f) {}

        void F()
        {
            G2(a => a);
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaParameters, "a", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Generic1()
        {
            var src1 = @"
delegate T D1<S, T>(S a, T b);
delegate T D2<S, T>(T a, S b);

class C
{
    void G1(D1<int, int> f) {}
    void G2(D2<int, int> f) {}

    void F()
    {
        G1((a, b) => a + b);
    }
}
";
            var src2 = @"
delegate T D1<S, T>(S a, T b);
delegate T D2<S, T>(T a, S b);

class C
{
    void G1(D1<int, int> f) {}
    void G2(D2<int, int> f) {}

    void F()
    {
        G2((a, b) => a + b);
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_Generic2()
        {
            var src1 = @"
delegate int D1<S, T>(S a, T b);
delegate int D2<S, T>(T a, S b);

class C
{
    void G1(D1<int, int> f) {}
    void G2(D2<int, string> f) {}

    void F()
    {
        G1((a, b) => 1);
    }
}
";
            var src2 = @"
delegate int D1<S, T>(S a, T b);
delegate int D2<S, T>(T a, S b);

class C
{
    void G1(D1<int, int> f) {}
    void G2(D2<int, string> f) {}

    void F()
    {
        G2((a, b) => 1);
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingLambdaParameters, "(a, b)", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_CapturedParameters1()
        {
            var src1 = @"
using System;

class C
{
    void F(int x1)
    {
        var f1 = new Func<int, int, int>((a1, a2) => 
        {
            var f2 = new Func<int, int>(a3 => x1 + a2);
            return a1;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    void F(int x1)
    {
        var f1 = new Func<int, int, int>((a1, a2) => 
        {
            var f2 = new Func<int, int>(a3 => x1 + a2 + 1);
            return a1;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact, WorkItem(2223, "https://github.com/dotnet/roslyn/issues/2223")]
        public void Lambdas_Update_CapturedParameters2()
        {
            var src1 = @"
using System;

class C
{
    void F(int x1)
    {
        var f1 = new Func<int, int, int>((a1, a2) => 
        {
            var f2 = new Func<int, int>(a3 => x1 + a2);
            return a1;
        });

        var f3 = new Func<int, int, int>((a1, a2) => 
        {
            var f4 = new Func<int, int>(a3 => x1 + a2);
            return a1;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    void F(int x1)
    {
        var f1 = new Func<int, int, int>((a1, a2) => 
        {
            var f2 = new Func<int, int>(a3 => x1 + a2 + 1);
            return a1;
        });

        var f3 = new Func<int, int, int>((a1, a2) => 
        {
            var f4 = new Func<int, int>(a3 => x1 + a2 + 1);
            return a1;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_CeaseCapture_Closure1()
        {
            var src1 = @"
using System;

class C
{
    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => 
        {
            var f2 = new Func<int, int>(a2 => y + a2);
            return a1;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => 
        {
            var f2 = new Func<int, int>(a2 => a2);
            return a1 + y;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            // y is no longer captured in f2
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotAccessingCapturedVariableInLambda, "a2", "y", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_CeaseCapture_IndexerParameter1()
        {
            var src1 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] => new Func<int, int>(a3 => a1 + a2);
}
";
            var src2 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] => new Func<int, int>(a3 => a2);
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "a1", "a1"));
        }

        [Fact]
        public void Lambdas_Update_CeaseCapture_IndexerParameter2()
        {
            var src1 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] { get { return new Func<int, int>(a3 => a1 + a2); } }
}
";
            var src2 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] { get { return new Func<int, int>(a3 => a2); } }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "a1", "a1"));
        }

        [Fact]
        public void Lambdas_Update_CeaseCapture_MethodParameter1()
        {
            var src1 = @"
using System;

class C
{
    void F(int a1, int a2)
    {
        var f2 = new Func<int, int>(a3 => a1 + a2);
    }
}
";
            var src2 = @"
using System;

class C
{
    void F(int a1, int a2)
    {
        var f2 = new Func<int, int>(a3 => a1);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "a2", "a2"));
        }

        [Fact]
        public void Lambdas_Update_CeaseCapture_MethodParameter2()
        {
            var src1 = @"
using System;

class C
{
    Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1 + a2);
}
";
            var src2 = @"
using System;

class C
{
    Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1);
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "a2", "a2"));
        }

        [Fact]
        public void Lambdas_Update_CeaseCapture_LambdaParameter1()
        {
            var src1 = @"
using System;

class C
{
    void F()
    {
        var f1 = new Func<int, int, int>((a1, a2) => 
        {
            var f2 = new Func<int, int>(a3 => a1 + a2);
            return a1;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    void F()
    {
        var f1 = new Func<int, int, int>((a1, a2) => 
        {
            var f2 = new Func<int, int>(a3 => a2);
            return a1;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "a1", "a1"));
        }

        [Fact, WorkItem(234448, "https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")]
        public void Lambdas_Update_CeaseCapture_SetterValueParameter1()
        {
            var src1 = @"
using System;

class C
{
    int D
    {
        get { return 0; }
        set { new Action(() => { Console.Write(value); }).Invoke(); }
    }
}
";
            var src2 = @"
using System;

class C
{
    int D
    {
        get { return 0; }
        set { }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "set", "value"));
        }

        [Fact, WorkItem(234448, "https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")]
        public void Lambdas_Update_CeaseCapture_IndexerSetterValueParameter1()
        {
            var src1 = @"
using System;

class C
{
    int this[int a1, int a2]
    {
        get { return 0; }
        set { new Action(() => { Console.Write(value); }).Invoke(); }
    }
}
";
            var src2 = @"
using System;

class C
{
    int this[int a1, int a2]
    {
        get { return 0; }
        set { }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "set", "value"));
        }

        [Fact, WorkItem(234448, "https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")]
        public void Lambdas_Update_CeaseCapture_EventAdderValueParameter1()
        {
            var src1 = @"
using System;

class C
{
    event Action D
    {
        add { new Action(() => { Console.Write(value); }).Invoke(); }
        remove { }
    }
}
";
            var src2 = @"
using System;

class C
{
    event Action D
    {
        add {  }
        remove { }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "add", "value"));
        }

        [Fact, WorkItem(234448, "https://devdiv.visualstudio.com/DevDiv/_workitems?id=234448")]
        public void Lambdas_Update_CeaseCapture_EventRemoverValueParameter1()
        {
            var src1 = @"
using System;

class C
{
    event Action D
    {
        add {  }
        remove { new Action(() => { Console.Write(value); }).Invoke(); }
    }
}
";
            var src2 = @"
using System;

class C
{
    event Action D
    {
        add { }
        remove { }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "remove", "value"));
        }

        [Fact]
        public void Lambdas_Update_DeleteCapture1()
        {
            var src1 = @"
using System;

class C
{
    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => 
        {
            var f2 = new Func<int, int>(a2 => y + a2);
            return y;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    void F()
    { // error
        var f1 = new Func<int, int>(a1 => 
        {
            var f2 = new Func<int, int>(a2 => a2);
            return a1;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            // y is no longer captured in f2
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.DeletingCapturedVariable, "{", "y").WithFirstLine("{ // error"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_IndexerGetterParameter1()
        {
            var src1 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] => new Func<int, int>(a3 => a2);
}
";
            var src2 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] => new Func<int, int>(a3 => a1 + a2);
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "a1", "a1"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_IndexerGetterParameter2()
        {
            var src1 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] { get { return new Func<int, int>(a3 => a2); } }
}
";
            var src2 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] { get { return new Func<int, int>(a3 => a1 + a2); } }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "a1", "a1"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_IndexerSetterParameter1()
        {
            var src1 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] { get { return null; } set { var f = new Func<int, int>(a3 => a2); } }
}
";
            var src2 = @"
using System;

class C
{
    Func<int, int> this[int a1, int a2] { get { return null; } set { var f = new Func<int, int>(a3 => a1 + a2); } }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "a1", "a1"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_IndexerSetterValueParameter1()
        {
            var src1 = @"
using System;

class C
{
    int this[int a1, int a2]
    {
        get { return 0; }
        set {  }
    }
}
";
            var src2 = @"
using System;

class C
{
    int this[int a1, int a2]
    {
        get { return 0; }
        set { new Action(() => { Console.Write(value); }).Invoke(); }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "set", "value"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_EventAdderValueParameter1()
        {
            var src1 = @"
using System;

class C
{
    event Action D
    {
        add {  }
        remove { }
    }
}
";
            var src2 = @"
using System;

class C
{
    event Action D
    {
        add {  }
        remove { new Action(() => { Console.Write(value); }).Invoke(); }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "remove", "value"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_EventRemoverValueParameter1()
        {
            var src1 = @"
using System;

class C
{
    event Action D
    {
        add {  }
        remove {  }
    }
}
";
            var src2 = @"
using System;

class C
{
    event Action D
    {
        add { }
        remove { new Action(() => { Console.Write(value); }).Invoke(); }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "remove", "value"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_MethodParameter1()
        {
            var src1 = @"
using System;

class C
{
    void F(int a1, int a2)
    {
        var f2 = new Func<int, int>(a3 => a1);
    }
}
";
            var src2 = @"
using System;

class C
{
    void F(int a1, int a2)
    {
        var f2 = new Func<int, int>(a3 => a1 + a2);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "a2", "a2"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_MethodParameter2()
        {
            var src1 = @"
using System;

class C
{
    Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1);
}
";
            var src2 = @"
using System;

class C
{
    Func<int, int> F(int a1, int a2) => new Func<int, int>(a3 => a1 + a2);
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "a2", "a2"));
        }

        [Fact]
        public void Lambdas_Update_Capturing_LambdaParameter1()
        {
            var src1 = @"
using System;

class C
{
    void F()
    {
        var f1 = new Func<int, int, int>((a1, a2) => 
        {
            var f2 = new Func<int, int>(a3 => a2);
            return a1;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    void F()
    {
        var f1 = new Func<int, int, int>((a1, a2) => 
        {
            var f2 = new Func<int, int>(a3 => a1 + a2);
            return a1;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "a1", "a1"));
        }

        [Fact]
        public void Lambdas_Update_StaticToThisOnly1()
        {
            var src1 = @"
using System;

class C
{
    int x = 1;

    void F()
    {
        var f = new Func<int, int>(a => a);
    }
}
";
            var src2 = @"
using System;

class C
{
    int x = 1;
   
    void F()
    {
        var f = new Func<int, int>(a => a + x);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "F", "this"));
        }

        [Fact]
        public void Lambdas_Update_StaticToThisOnly_Partial()
        {
            var src1 = @"
using System;

partial class C
{
    int x = 1;
    partial void F(); // def
}

partial class C
{
    partial void F()  // impl
    {
        var f = new Func<int, int>(a => a);
    }
}
";
            var src2 = @"
using System;

partial class C
{
    int x = 1;
    partial void F(); // def
}

partial class C
{
    partial void F()  // impl
    {
        var f = new Func<int, int>(a => a + x);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "F", "this").WithFirstLine("partial void F()  // impl"));
        }

        [Fact]
        public void Lambdas_Update_StaticToThisOnly3()
        {
            var src1 = @"
using System;

class C
{
    int x = 1;

    void F()
    {
        var f1 = new Func<int, int>(a1 => a1);
        var f2 = new Func<int, int>(a2 => a2 + x);
    }
}
";
            var src2 = @"
using System;

class C
{
    int x = 1;
   
    void F()
    {
        var f1 = new Func<int, int>(a1 => a1 + x);
        var f2 = new Func<int, int>(a2 => a2 + x);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "a1", "this", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_StaticToClosure1()
        {
            var src1 = @"
using System;

class C
{
    void F()
    {
        int x = 1;
        var f1 = new Func<int, int>(a1 => a1);
        var f2 = new Func<int, int>(a2 => a2 + x);
    }
}
";
            var src2 = @"
using System;

class C
{
    void F()
    {
        int x = 1;
        var f1 = new Func<int, int>(a1 => 
        { 
            return a1 + 
                x+ // 1 
                x; // 2
        });

        var f2 = new Func<int, int>(a2 => a2 + x);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "x", "x", CSharpFeaturesResources.lambda).WithFirstLine("x+ // 1"),
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "x", "x", CSharpFeaturesResources.lambda).WithFirstLine("x; // 2"));
        }

        [Fact]
        public void Lambdas_Update_ThisOnlyToClosure1()
        {
            var src1 = @"
using System;

class C
{
    int x = 1;

    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => a1 + x);
        var f2 = new Func<int, int>(a2 => a2 + x + y);
    }
}
";
            var src2 = @"
using System;

class C
{
    int x = 1;

    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => a1 + x + y);
        var f2 = new Func<int, int>(a2 => a2 + x + y);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "y", "y", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Nested1()
        {
            var src1 = @"
using System;

class C
{
    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => 
        {
            var f2 = new Func<int, int>(a2 => a2 + y);
            return a1;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => 
        {
            var f2 = new Func<int, int>(a2 => a2 + y);
            return a1 + y;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Lambdas_Update_Nested2()
        {
            var src1 = @"
using System;

class C
{
    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => 
        {
            var f2 = new Func<int, int>(a2 => a2);
            return a1;
        });
    }
}
";
            var src2 = @"
using System;

class C
{
    void F()
    {
        int y = 1;
        var f1 = new Func<int, int>(a1 => 
        {
            var f2 = new Func<int, int>(a2 => a1 + a2);
            return a1;
        });
    }
}
";
            var edits = GetTopEdits(src1, src2);

            // TODO: better diagnostics - identify a1 that causes the capture vs. a1 that doesn't
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "a1", "a1").WithFirstLine("var f1 = new Func<int, int>(a1 =>"));
        }

        [Fact]
        public void Lambdas_Update_Accessing_Closure1()
        {
            var src1 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    void F()
    {
        int x0 = 0, y0 = 0;                
                                         
        G(a => x0);
        G(a => y0);
    }
}
";
            var src2 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    void F()
    {
        int x0 = 0, y0 = 0;                
                                         
        G(a => x0);
        G(a => y0 + x0);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "x0", "x0", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Accessing_Closure2()
        {
            var src1 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    int x = 0;                     // Group #0
                                   
    void F()                       
    {                              
        { int x0 = 0, y0 = 0;      // Group #0             
            { int x1 = 0, y1 = 0;  // Group #1               
                                         
                G(a => x + x0);   
                G(a => x0);
                G(a => y0);
                G(a => x1);
            }
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    void G(Func<int, int> f) {}
    int x = 0;                     // Group #0

    void F()
    {
        { int x0 = 0, y0 = 0;      // Group #0          
            { int x1 = 0, y1 = 0;  // Group #1              
                                         
                G(a => x);         // error: disconnecting previously connected closures
                G(a => x0);
                G(a => y0);
                G(a => x1);
            }
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotAccessingCapturedVariableInLambda, "a", "x0", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Accessing_Closure3()
        {
            var src1 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    int x = 0;                     // Group #0
                                   
    void F()                       
    {                              
        { int x0 = 0, y0 = 0;      // Group #0             
            { int x1 = 0, y1 = 0;  // Group #1               
                                         
                G(a => x);   
                G(a => x0);
                G(a => y0);
                G(a => x1);
                G(a => y1);
            }
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    void G(Func<int, int> f) {}
    int x = 0;                     // Group #0

    void F()
    {
        { int x0 = 0, y0 = 0;      // Group #0          
            { int x1 = 0, y1 = 0;  // Group #1              
                                         
                G(a => x);         
                G(a => x0);
                G(a => y0);
                G(a => x1);
                G(a => y1 + x0);   // error: connecting previously disconnected closures
            }
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "x0", "x0", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Lambdas_Update_Accessing_Closure4()
        {
            var src1 = @"
using System;

class C
{
    void G(Func<int, int> f) {}

    int x = 0;                     // Group #0
                                   
    void F()                       
    {                              
        { int x0 = 0, y0 = 0;      // Group #0             
            { int x1 = 0, y1 = 0;  // Group #1               
                                         
                G(a => x + x0);   
                G(a => x0);
                G(a => y0);
                G(a => x1);
                G(a => y1);
            }
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    void G(Func<int, int> f) {}
    int x = 0;                     // Group #0

    void F()
    {
        { int x0 = 0, y0 = 0;      // Group #0          
            { int x1 = 0, y1 = 0;  // Group #1              
                                         
                G(a => x);         // error: disconnecting previously connected closures
                G(a => x0);
                G(a => y0);
                G(a => x1);
                G(a => y1 + x0);   // error: connecting previously disconnected closures
            }
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            // TODO: "a => x + x0" is matched with "a => y1 + x0", hence we report more errors.
            // Including statement distance when matching would help.
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotAccessingCapturedVariableInLambda, "a", "this", CSharpFeaturesResources.lambda).WithFirstLine("G(a => y1 + x0);   // error: connecting previously disconnected closures"),
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "y1", "y1", CSharpFeaturesResources.lambda).WithFirstLine("G(a => y1 + x0);   // error: connecting previously disconnected closures"),
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "a", "this", CSharpFeaturesResources.lambda).WithFirstLine("G(a => x);         // error: disconnecting previously connected closures"),
                Diagnostic(RudeEditKind.NotAccessingCapturedVariableInLambda, "a", "y1", CSharpFeaturesResources.lambda).WithFirstLine("G(a => x);         // error: disconnecting previously connected closures"));
        }

        [Fact]
        public void Lambdas_Update_Accessing_Closure_NestedLambdas()
        {
            var src1 = @"
using System;

class C
{
    void G(Func<int, Func<int, int>> f) {}

    void F()                       
    {                              
        { int x0 = 0;      // Group #0             
            { int x1 = 0;  // Group #1               
                                         
                G(a => b => x0);
                G(a => b => x1);
            }
        }
    }
}
";
            var src2 = @"
using System;

class C
{
    void G(Func<int, Func<int, int>> f) {}

    void F()
    {
        { int x0 = 0;      // Group #0          
            { int x1 = 0;  // Group #1              
                                         
                G(a => b => x0);
                G(a => b => x1);

                G(a => b => x0);      // ok
                G(a => b => x1);      // ok
                G(a => b => x0 + x1); // error
            }
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x1", CSharpFeaturesResources.lambda, "x0", "x1"),
                Diagnostic(RudeEditKind.InsertLambdaWithMultiScopeCapture, "x1", CSharpFeaturesResources.lambda, "x0", "x1"));
        }

        [Fact]
        public void Lambdas_RenameCapturedLocal()
        {
            string src1 = @"
using System;
using System.Diagnostics;

class Program
{
    static void Main()
    {
        int x = 1;
        Func<int> f = () => x;
    }
}";
            string src2 = @"
using System;
using System.Diagnostics;

class Program
{
    static void Main()
    {
        int X = 1;
        Func<int> f = () => X;
    }
}";

            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.RenamingCapturedVariable, "X", "x", "X"));
        }

        [Fact]
        public void Lambdas_RenameCapturedParameter()
        {
            string src1 = @"
using System;
using System.Diagnostics;

class Program
{
    static void Main(int x)
    {
        Func<int> f = () => x;
    }
}";
            string src2 = @"
using System;
using System.Diagnostics;

class Program
{
    static void Main(int X)
    {
        Func<int> f = () => X;
    }
}";

            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Renamed, "int X", "parameter"));
        }

        #endregion

        #region Queries

        [Fact]
        public void Queries_Update_Signature_Select1()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} select a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1.0} select a;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "select", CSharpFeaturesResources.select_clause));
        }

        [Fact]
        public void Queries_Update_Signature_Select2()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} select a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} select a.ToString();
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "select", CSharpFeaturesResources.select_clause));
        }

        [Fact]
        public void Queries_Update_Signature_From1()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} from b in new[] {2} select b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from long a in new[] {1} from b in new[] {2} select b;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "from", CSharpFeaturesResources.from_clause));
        }

        [Fact]
        public void Queries_Update_Signature_From2()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from System.Int64 a in new[] {1} from b in new[] {2} select b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from long a in new[] {1} from b in new[] {2} select b;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_Update_Signature_From3()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} from b in new[] {2} select b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new List<int>() from b in new List<int>() select b;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_Update_Signature_Let1()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} let b = 1 select a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} let b = 1.0 select a;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "let", CSharpFeaturesResources.let_clause));
        }

        [Fact]
        public void Queries_Update_Signature_OrderBy1()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} orderby a + 1 descending, a + 2 ascending select a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} orderby a + 1.0 descending, a + 2 ascending select a;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "a + 1.0 descending", CSharpFeaturesResources.orderby_clause));
        }

        [Fact]
        public void Queries_Update_Signature_OrderBy2()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} orderby a + 1 descending, a + 2 ascending select a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} orderby a + 1 descending, a + 2.0 ascending select a;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "a + 2.0 ascending", CSharpFeaturesResources.orderby_clause));
        }

        [Fact]
        public void Queries_Update_Signature_Join1()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} join b in new[] {1} on a equals b select b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} join b in new[] {1.0} on a equals b select b;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "join", CSharpFeaturesResources.join_clause));
        }

        [Fact]
        public void Queries_Update_Signature_Join2()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} join b in new[] {1} on a equals b select b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} join byte b in new[] {1} on a equals b select b;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "join", CSharpFeaturesResources.join_clause));
        }

        [Fact]
        public void Queries_Update_Signature_Join3()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} join b in new[] {1} on a + 1 equals b select b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} join b in new[] {1} on a + 1.0 equals b select b;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "join", CSharpFeaturesResources.join_clause));
        }

        [Fact]
        public void Queries_Update_Signature_Join4()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} join b in new[] {1} on a equals b + 1 select b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} join b in new[] {1} on a equals b + 1.0 select b;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "join", CSharpFeaturesResources.join_clause));
        }

        [Fact]
        public void Queries_Update_Signature_GroupBy1()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a + 1 by a into z select z;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a + 1.0 by a into z select z;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "group", CSharpFeaturesResources.groupby_clause));
        }

        [Fact]
        public void Queries_Update_Signature_GroupBy2()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a by a into z select z;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a by a + 1.0 into z select z;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "group", CSharpFeaturesResources.groupby_clause));
        }

        [Fact]
        public void Queries_FromSelect_Update1()
        {
            var src1 = "F(from a in b from x in y select c);";
            var src2 = "F(from a in c from x in z select c + 1);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [from a in b]@4 -> [from a in c]@4",
                "Update [from x in y]@16 -> [from x in z]@16",
                "Update [select c]@28 -> [select c + 1]@28");
        }

        [Fact]
        public void Queries_FromSelect_Update2()
        {
            var src1 = "F(from a in b from x in y select c);";
            var src2 = "F(from a in b from x in z select c);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [from x in y]@16 -> [from x in z]@16");
        }

        [Fact]
        public void Queries_FromSelect_Update3()
        {
            var src1 = "F(from a in await b from x in y select c);";
            var src2 = "F(from a in await c from x in y select c);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [await b]@14 -> [await c]@14");
        }

        [Fact]
        public void Queries_Select_Reduced1()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} where a > 0 select a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} where a > 0 select a + 1;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_Select_Reduced2()
        {
            var src1 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} where a > 0 select a + 1;
    }
}
";
            var src2 = @"
using System;
using System.Linq;
using System.Collections.Generic;

class C
{
    void F()
    {
        var result = from a in new[] {1} where a > 0 select a;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_FromSelect_Delete()
        {
            var src1 = "F(from a in b from c in d select a + c);";
            var src2 = "F(from a in b select c + 1);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [select a + c]@28 -> [select c + 1]@16",
                "Delete [from c in d]@16");
        }

        [Fact]
        public void Queries_JoinInto_Update()
        {
            var src1 = "F(from a in b join b in c on a equals b into g1 select g1);";
            var src2 = "F(from a in b join b in c on a equals b into g2 select g2);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [select g1]@50 -> [select g2]@50",
                "Update [into g1]@42 -> [into g2]@42");
        }

        [Fact]
        public void Queries_JoinIn_Update()
        {
            var src1 = "F(from a in b join b in await A(1) on a equals b select g);";
            var src2 = "F(from a in b join b in await A(2) on a equals b select g);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [await A(1)]@26 -> [await A(2)]@26");
        }

        [Fact]
        public void Queries_GroupBy_Update()
        {
            var src1 = "F(from a in b  group a by a.x into g  select g);";
            var src2 = "F(from a in b  group z by z.y into h  select h);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [group a by a.x]@17 -> [group z by z.y]@17",
                "Update [into g  select g]@32 -> [into h  select h]@32",
                "Update [select g]@40 -> [select h]@40");
        }

        [Fact]
        public void Queries_OrderBy_Reorder()
        {
            var src1 = "F(from a in b  orderby a.x, a.b descending, a.c ascending  select a.d);";
            var src2 = "F(from a in b  orderby a.x, a.c ascending, a.b descending  select a.d);";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [a.c ascending]@46 -> @30");
        }

        [Fact]
        public void Queries_GroupBy_Reduced1()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a by a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a + 1.0 by a;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "group", CSharpFeaturesResources.groupby_clause));
        }

        [Fact]
        public void Queries_GroupBy_Reduced2()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a by a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a + 1 by a;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_GroupBy_Reduced3()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a + 1.0 by a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a by a;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.ChangingQueryLambdaType, "group", CSharpFeaturesResources.groupby_clause));
        }

        [Fact]
        public void Queries_GroupBy_Reduced4()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a + 1 by a;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] {1} group a by a;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_OrderBy_Continuation_Update()
        {
            var src1 = "F(from a in b  orderby a.x, a.b descending  select a.d  into z  orderby a.c ascending select z);";
            var src2 = "F(from a in b  orderby a.x, a.c ascending  select a.d  into z  orderby a.b descending select z);";

            var edits = GetMethodEdits(src1, src2);

            var actual = ToMatchingPairs(edits.Match);

            var expected = new MatchingPairs
            {
                { "F(from a in b  orderby a.x, a.b descending  select a.d  into z  orderby a.c ascending select z);", "F(from a in b  orderby a.x, a.c ascending  select a.d  into z  orderby a.b descending select z);" },
                { "from a in b", "from a in b" },
                { "orderby a.x, a.b descending  select a.d  into z  orderby a.c ascending select z", "orderby a.x, a.c ascending  select a.d  into z  orderby a.b descending select z" },
                { "orderby a.x, a.b descending", "orderby a.x, a.c ascending" },
                { "a.x", "a.x" },
                { "a.b descending", "a.c ascending" },
                { "select a.d", "select a.d" },
                { "into z  orderby a.c ascending select z", "into z  orderby a.b descending select z" },
                { "orderby a.c ascending select z", "orderby a.b descending select z" },
                { "orderby a.c ascending", "orderby a.b descending" },
                { "a.c ascending", "a.b descending" },
                { "select z", "select z" }
            };

            expected.AssertEqual(actual);

            edits.VerifyEdits(
                "Update [a.b descending]@30 -> [a.c ascending]@30",
                "Update [a.c ascending]@74 -> [a.b descending]@73");
        }

        [Fact]
        public void Queries_CapturedTransparentIdentifiers_FromClause1()
        {
            string src1 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
		             from b in new[] { 2 }
		             where Z(() => a) > 0
		             where Z(() => b) > 0
		             where Z(() => a) > 0
		             where Z(() => b) > 0
		             select a;
    }
}";
            string src2 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
		             from b in new[] { 2 }
		             where Z(() => a) > 1  // update
		             where Z(() => b) > 2  // update
		             where Z(() => a) > 3  // update
		             where Z(() => b) > 4  // update
		             select a;
    }
}";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_CapturedTransparentIdentifiers_LetClause1()
        {
            string src1 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
		             let b = Z(() => a)
		             select a + b;
    }
}";
            string src2 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
		             let b = Z(() => a + 1)
		             select a - b;
    }
}";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_CapturedTransparentIdentifiers_JoinClause1()
        {
            string src1 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
                     join b in new[] { 3 } on Z(() => a + 1) equals Z(() => b - 1) into g
                     select Z(() => g.First());
    }
}";
            string src2 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
                     join b in new[] { 3 } on Z(() => a + 1) equals Z(() => b - 1) into g
                     select Z(() => g.Last());
    }
}";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_CeaseCapturingTransparentIdentifiers1()
        {
            string src1 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
		             from b in new[] { 2 }
		             where Z(() => a + b) > 0
		             select a;
    }
}";
            string src2 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
		             from b in new[] { 2 }
		             where Z(() => a + 1) > 0
		             select a;
    }
}";
            var edits = GetTopEdits(src1, src2);

            // TODO: better location (the variable, not the from clause)
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotCapturingVariable, "from b in new[] { 2 }", "b"));
        }

        [Fact]
        public void Queries_CapturingTransparentIdentifiers1()
        {
            string src1 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
		             from b in new[] { 2 }
		             where Z(() => a + 1) > 0
		             select a;
    }
}";
            string src2 = @"
using System;
using System.Linq;

class C
{
	int Z(Func<int> f)
	{
		return 1;
	}

    void F()
    {
		var result = from a in new[] { 1 }
		             from b in new[] { 2 }
		             where Z(() => a + b) > 0
		             select a;
    }
}";
            var edits = GetTopEdits(src1, src2);

            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.CapturingVariable, "b", "b"));
        }

        [Fact]
        public void Queries_AccessingCapturedTransparentIdentifier1()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;

    void F()
    {
        var result = from a in new[] { 1 }
                     where Z(() => a) > 0
                     select 1;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;
   
    void F()
    {
        var result = from a in new[] { 1 } 
                     where Z(() => a) > 0
                     select a;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        [Fact]
        public void Queries_AccessingCapturedTransparentIdentifier2()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;

    void F()
    {
        var result = from a in new[] { 1 }
                     from b in new[] { 1 }
                     where Z(() => a) > 0
                     select b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;
   
    void F()
    {
        var result = from a in new[] { 1 } 
                     from b in new[] { 1 }
                     where Z(() => a) > 0
                     select a + b;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "a", "a", CSharpFeaturesResources.select_clause));
        }

        [Fact]
        public void Queries_AccessingCapturedTransparentIdentifier3()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;

    void F()
    {
        var result = from a in new[] { 1 }
                     where Z(() => a) > 0
                     select Z(() => 1);
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;
   
    void F()
    {
        var result = from a in new[] { 1 } 
                     where Z(() => a) > 0
                     select Z(() => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "a", "a", CSharpFeaturesResources.select_clause),
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "a", "a", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Queries_NotAccessingCapturedTransparentIdentifier1()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;

    void F()
    {
        var result = from a in new[] { 1 }
                     from b in new[] { 1 }
                     where Z(() => a) > 0
                     select a + b;
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;
   
    void F()
    {
        var result = from a in new[] { 1 } 
                     from b in new[] { 1 }
                     where Z(() => a) > 0
                     select b;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.NotAccessingCapturedVariableInLambda, "select", "a", CSharpFeaturesResources.select_clause));
        }

        [Fact]
        public void Queries_NotAccessingCapturedTransparentIdentifier2()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;

    void F()
    {
        var result = from a in new[] { 1 }
                     where Z(() => a) > 0
                     select Z(() => 1);
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    int Z(Func<int> f) => 1;
   
    void F()
    {
        var result = from a in new[] { 1 } 
                     where Z(() => a) > 0
                     select Z(() => a);
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics(
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "a", "a", CSharpFeaturesResources.select_clause),
                Diagnostic(RudeEditKind.AccessingCapturedVariableInLambda, "a", "a", CSharpFeaturesResources.lambda));
        }

        [Fact]
        public void Queries_Insert1()
        {
            var src1 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
    }
}
";
            var src2 = @"
using System;
using System.Linq;

class C
{
    void F()
    {
        var result = from a in new[] { 1 } select a;
    }
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifySemanticDiagnostics();
        }

        #endregion

        #region Yield

        [Fact]
        public void Yield_Update1()
        {
            var src1 = @"
class C
{
    static IEnumerable<int> F()
    {
        yield return 1;
        yield return 2;
        yield break;
    }
}
";
            var src2 = @"
class C
{
    static IEnumerable<int> F()
    {
        yield return 3;
        yield break;
        yield return 4;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Update, "yield break;", CSharpFeaturesResources.yield_statement),
                Diagnostic(RudeEditKind.Update, "yield return 4;", CSharpFeaturesResources.yield_statement));
        }

        [Fact]
        public void Yield_Delete1()
        {
            var src1 = @"
yield return 1;
yield return 2;
yield return 3;
";
            var src2 = @"
yield return 1;
yield return 3;
";

            var bodyEdits = GetMethodEdits(src1, src2, kind: MethodKind.Iterator);

            bodyEdits.VerifyEdits(
                "Delete [yield return 2;]@42");
        }

        [Fact]
        public void Yield_Delete2()
        {
            var src1 = @"
class C
{
    static IEnumerable<int> F()
    {
        yield return 1;
        yield return 2;
        yield return 3;
    }
}
";
            var src2 = @"
class C
{
    static IEnumerable<int> F()
    {
        yield return 1;
        yield return 3;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Delete, "{", CSharpFeaturesResources.yield_statement));
        }

        [Fact]
        public void Yield_Insert1()
        {
            var src1 = @"
yield return 1;
yield return 3;
";
            var src2 = @"
yield return 1;
yield return 2;
yield return 3;
yield return 4;
";

            var bodyEdits = GetMethodEdits(src1, src2, kind: MethodKind.Iterator);

            bodyEdits.VerifyEdits(
                "Insert [yield return 2;]@42",
                "Insert [yield return 4;]@76");
        }

        [Fact]
        public void Yield_Insert2()
        {
            var src1 = @"
class C
{
    static IEnumerable<int> F()
    {
        yield return 1;
        yield return 3;
    }
}
";
            var src2 = @"
class C
{
    static IEnumerable<int> F()
    {
        yield return 1;
        yield return 2;
        yield return 3;
        yield return 4;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Insert, "yield return 4;", CSharpFeaturesResources.yield_statement),
                Diagnostic(RudeEditKind.Insert, "yield return 2;", CSharpFeaturesResources.yield_statement));
        }

        [Fact]
        public void MissingIteratorStateMachineAttribute()
        {
            var src1 = @"
using System.Collections.Generic;

class C
{
    static IEnumerable<int> F()
    {
        yield return 1;
    }
}
";
            var src2 = @"
using System.Collections.Generic;

class C
{
    static IEnumerable<int> F()
    {
        yield return 2;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            CSharpEditAndContinueTestHelpers.Instance40.VerifySemantics(
                edits,
                ActiveStatementsDescription.Empty,
                null,
                null,
                null,
                new[]
                {
                    Diagnostic(RudeEditKind.UpdatingStateMachineMethodMissingAttribute, "static IEnumerable<int> F()", "System.Runtime.CompilerServices.IteratorStateMachineAttribute")
                });
        }

        [Fact]
        public void MissingIteratorStateMachineAttribute2()
        {
            var src1 = @"
using System.Collections.Generic;

class C
{
    static IEnumerable<int> F()
    {
        return null;
    }
}
";
            var src2 = @"
using System.Collections.Generic;

class C
{
    static IEnumerable<int> F()
    {
        yield return 2;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            CSharpEditAndContinueTestHelpers.Instance40.VerifySemantics(
                edits,
                ActiveStatementsDescription.Empty,
                null,
                null,
                null,
                null);
        }

        [Fact]
        public void CSharp7SwitchStatement()
        {
            var src1 = @"
class C
{
    static void F(object o)
    {
        switch (o)
        {
            case int i:
                break;
        }
        System.Console.WriteLine(1);
    }
}
";
            var src2 = @"
class C
{
    static void F(object o)
    {
        switch (o)
        {
            case int i:
                break;
        }
        System.Console.WriteLine(2);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            CSharpEditAndContinueTestHelpers.Instance.VerifySemantics(
                edits,
                ActiveStatementsDescription.Empty,
                expectedDiagnostics: new[]
                {
                    Diagnostic(RudeEditKind.UpdateAroundActiveStatement, null, CSharpFeaturesResources.v7_switch)
                });
        }

        [Fact]
        public void AddCSharp7SwitchStatement()
        {
            var src1 = @"
class C
{
    static void F(object o)
    {
    }
}
";
            var src2 = @"
class C
{
    static void F(object o)
    {
        switch (o)
        {
            case string s:
                break;
        }
        switch (o)
        {
            case int i:
                break;
        }
    }
}
";
            var edits = GetTopEdits(src1, src2);

            CSharpEditAndContinueTestHelpers.Instance.VerifySemantics(
                edits,
                ActiveStatementsDescription.Empty,
                additionalOldSources: null,
                additionalNewSources: null,
                expectedSemanticEdits: null,
                expectedDiagnostics: new[]
                {
                    Diagnostic(RudeEditKind.UpdateAroundActiveStatement, "switch (o)", CSharpFeaturesResources.v7_switch)
                });
        }

        [Fact]
        public void AddCSharp7SwitchStatement2()
        {
            var src1 = @"
class C
{
    static void F(object o)
    {
        switch (o)
        {
            case 1:
            case """":
                break;
        }
    }
}
";
            var src2 = @"
class C
{
    static void F(object o)
    {
    }
}
";
            var edits = GetTopEdits(src1, src2);

            CSharpEditAndContinueTestHelpers.Instance.VerifySemantics(
                edits,
                ActiveStatementsDescription.Empty,
                additionalOldSources: null,
                additionalNewSources: null,
                expectedSemanticEdits: null,
                expectedDiagnostics: new[]
                {
                    Diagnostic(RudeEditKind.UpdateAroundActiveStatement, null, CSharpFeaturesResources.v7_switch)
                });
        }

        #endregion

        #region Await

        [Fact]
        public void Await_Update_OK()
        {
            var src1 = @"
class C
{
    static async Task<int> F()
    {
        await F(1);
        if (await F(1)) { Console.WriteLine(1); }
        if (await F(1)) { Console.WriteLine(1); }
        if (F(1, await F(1))) { Console.WriteLine(1); }
        if (await F(1)) { Console.WriteLine(1); } 
        do { Console.WriteLine(1); } while (await F(1));  
        for (var x = await F(1); await G(1); await H(1)) { Console.WriteLine(1); } 
        foreach (var x in await F(1)) { Console.WriteLine(1); } 
        using (var x = await F(1)) { Console.WriteLine(1); } 
        lock (await F(1)) { Console.WriteLine(1); } 
        lock (a = await F(1)) { Console.WriteLine(1); } 
        var a = await F(1), b = await G(1);
        a = await F(1);
        switch (await F(2)) { case 1: return b = await F(1); }
        return await F(1);
    }

    static async Task<int> G() => await F(1);
}
";
            var src2 = @"
class C
{
    static async Task<int> F()
    {
        await F(2);
        if (await F(1)) { Console.WriteLine(2); }        
        if (await F(2)) { Console.WriteLine(1); }       
        if (F(1, await F(1))) { Console.WriteLine(2); } 
        while (await F(1)) { Console.WriteLine(1); }   
        do { Console.WriteLine(2); } while (await F(2));  
        for (var x = await F(2); await G(2); await H(2)) { Console.WriteLine(2); } 
        foreach (var x in await F(2)) { Console.WriteLine(2); } 
        using (var x = await F(2)) { Console.WriteLine(1); } 
        lock (await F(2)) { Console.WriteLine(2); } 
        lock (a = await F(2)) { Console.WriteLine(2); } 
        var a = await F(2), b = await G(2);
        b = await F(2);
        switch (await F(2)) { case 1: return b = await F(2); }
        return await F(2);
    }

    static async Task<int> G() => await F(2);
}
";
            var edits = GetTopEdits(src1, src2);
            edits.VerifyRudeDiagnostics();
        }

        [Fact]
        public void Await_Update_Errors()
        {
            var src1 = @"
class C
{
    static async Task<int> F()
    {
        F(1, await F(1));
        F(1, await F(1));
        F(await F(1));
        await F(await F(1));
        if (F(1, await F(1))) { Console.WriteLine(1); }
        var a = F(1, await F(1)), b = F(1, await G(1));
        b = F(1, await F(1));
        b += await F(1);
    }
}
";
            var src2 = @"
class C
{
    static async Task<int> F()
    {
        F(2, await F(1));                                
        F(1, await F(2));                                
        F(await F(2));                                   
        await F(await F(2));                            
        if (F(2, await F(1))) { Console.WriteLine(1); }  
        var a = F(1, await F(2)), b = F(1, await G(2));
        b = F(1, await F(2));
        b += await F(2);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            // consider: these edits can be allowed if we get more sophisticated
            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "F(2, await F(1));"),
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "F(1, await F(2));"),
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "F(await F(2));"),
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "await F(await F(2));"),
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "F(2, await F(1))"),
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "var a = F(1, await F(2)), b = F(1, await G(2));"),
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "var a = F(1, await F(2)), b = F(1, await G(2));"),
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "b = F(1, await F(2));"),
                Diagnostic(RudeEditKind.AwaitStatementUpdate, "b += await F(2);"));
        }

        [Fact]
        public void Await_Delete1()
        {
            var src1 = @"
await F(1);
await F(2);
await F(3);
";
            var src2 = @"
await F(1);
await F(3);
";

            var bodyEdits = GetMethodEdits(src1, src2, kind: MethodKind.Async);

            bodyEdits.VerifyEdits(
                "Delete [await F(2);]@37",
                "Delete [await F(2)]@37");
        }

        [Fact]
        public void Await_Delete2()
        {
            var src1 = @"
class C
{
    static async Task<int> F()
    {
        await F(1);
        {
            await F(2);
        }
        await F(3);
    }
}
";
            var src2 = @"
class C
{
    static async Task<int> F()
    {
        await F(1);
        {
            F(2);
        }
        await F(3);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Delete, "F(2);", CSharpFeaturesResources.await_expression));
        }

        [Fact]
        public void Await_Delete3()
        {
            var src1 = @"
class C
{
    static async Task<int> F()
    {
        await F(await F(1));
    }
}
";
            var src2 = @"
class C
{
    static async Task<int> F()
    {
        await F(1);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Delete, "await F(1);", CSharpFeaturesResources.await_expression));
        }

        [Fact]
        public void Await_Delete4()
        {
            var src1 = @"
class C
{
    static async Task<int> F() => await F(await F(1));
}
";
            var src2 = @"
class C
{
    static async Task<int> F() => await F(1);
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Delete, "=> await F(1)", CSharpFeaturesResources.await_expression));
        }

        [Fact]
        public void Await_Delete5()
        {
            var src1 = @"
class C
{
    static async Task<int> F() => await F(1);
}
";
            var src2 = @"
class C
{
    static Task<int> F() => F(1);
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(ActiveStatementsDescription.Empty,
                Diagnostic(RudeEditKind.Delete, "=> F(1)", CSharpFeaturesResources.await_expression),
                Diagnostic(RudeEditKind.ModifiersUpdate, "static Task<int> F()", FeaturesResources.method));
        }

        [Fact]
        public void Await_Delete6()
        {
            var src1 = @"
class C
{
    static async void F() => F();
}
";
            var src2 = @"
class C
{
    static void F() => F();
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(ActiveStatementsDescription.Empty,
                Diagnostic(RudeEditKind.ModifiersUpdate, "static void F()", "method"));
        }

        [Fact]
        public void Await_Insert1()
        {
            var src1 = @"
await F(1);
await F(3);
";
            var src2 = @"
await F(1);
await F(2);
await F(3);
await F(4);
";

            var bodyEdits = GetMethodEdits(src1, src2, kind: MethodKind.Async);

            bodyEdits.VerifyEdits(
                "Insert [await F(2);]@37",
                "Insert [await F(4);]@63",
                "Insert [await F(2)]@37",
                "Insert [await F(4)]@63");
        }

        [Fact]
        public void Await_Insert2()
        {
            var src1 = @"
class C
{
    static async IEnumerable<int> F()
    {
        await F(1);
        await F(3);
    }
}
";
            var src2 = @"
class C
{
    static async IEnumerable<int> F()
    {
        await F(1);
        await F(2);
        await F(3);
        await F(4);
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Insert, "await", CSharpFeaturesResources.await_expression),
                Diagnostic(RudeEditKind.Insert, "await", CSharpFeaturesResources.await_expression));
        }

        [Fact]
        public void Await_Insert3()
        {
            var src1 = @"
class C
{
    static async IEnumerable<int> F()
    {
        await F(1);
        await F(3);
    }
}
";
            var src2 = @"
class C
{
    static async IEnumerable<int> F()
    {
        await F(await F(1));
        await F(await F(2));
    }
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Delete, "await", CSharpFeaturesResources.await_expression),
                Diagnostic(RudeEditKind.Insert, "await", CSharpFeaturesResources.await_expression),
                Diagnostic(RudeEditKind.Insert, "await", CSharpFeaturesResources.await_expression),
                Diagnostic(RudeEditKind.Insert, "await", CSharpFeaturesResources.await_expression));
        }

        [Fact]
        public void Await_Insert4()
        {
            var src1 = @"
class C
{
    static async Task<int> F() => await F(1);
}
";
            var src2 = @"
class C
{
    static async Task<int> F() => await F(await F(1));
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics(
                Diagnostic(RudeEditKind.Insert, "await", CSharpFeaturesResources.await_expression));
        }

        [Fact]
        public void Await_Insert5()
        {
            var src1 = @"
class C
{
    static Task<int> F() => F(1);
}
";
            var src2 = @"
class C
{
    static async Task<int> F() => await F(1);
}
";
            var edits = GetTopEdits(src1, src2);

            edits.VerifyRudeDiagnostics();
        }

        [Fact]
        public void MissingAsyncStateMachineAttribute1()
        {
            var src1 = @"
using System.Threading.Tasks;

class C
{
    static async Task<int> F()
    {
        await new Task();
        return 1;
    }
}
";
            var src2 = @"
using System.Threading.Tasks;

class C
{
    static async Task<int> F()
    {
        await new Task();
        return 2;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            CSharpEditAndContinueTestHelpers.InstanceMinAsync.VerifySemantics(
                edits,
                ActiveStatementsDescription.Empty,
                null,
                null,
                null,
                new[]
                {
                    Diagnostic(RudeEditKind.UpdatingStateMachineMethodMissingAttribute, "static async Task<int> F()", "System.Runtime.CompilerServices.AsyncStateMachineAttribute")
                });
        }

        [Fact]
        public void MissingAsyncStateMachineAttribute2()
        {
            var src1 = @"
using System.Threading.Tasks;

class C
{
    static Task<int> F()
    {
        return null;
    }
}
";
            var src2 = @"
using System.Threading.Tasks;

class C
{
    static async Task<int> F()
    {
        await new Task();
        return 2;
    }
}
";
            var edits = GetTopEdits(src1, src2);

            CSharpEditAndContinueTestHelpers.InstanceMinAsync.VerifySemantics(
                edits,
                ActiveStatementsDescription.Empty,
                null,
                null,
                null,
                null);
        }

        #endregion

        #region Out Var

        [Fact]
        public void OutVarType_Update()
        {
            var src1 = @"
M(out var y);
";
            var src2 = @"
M(out int y);
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [M(out var y);]@4 -> [M(out int y);]@4");
        }

        [Fact]
        public void OutVarNameAndType_Update()
        {
            var src1 = @"
M(out var y);
";
            var src2 = @"
M(out int z);
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [M(out var y);]@4 -> [M(out int z);]@4",
                "Update [y]@14 -> [z]@14");
        }

        [Fact]
        public void OutVar_Insert()
        {
            var src1 = @"
M();
";
            var src2 = @"
M(out int y);
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [M();]@4 -> [M(out int y);]@4",
                "Insert [y]@14");
        }

        [Fact]
        public void OutVar_Delete()
        {
            var src1 = @"
M(out int y);
";

            var src2 = @"
M();
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [M(out int y);]@4 -> [M();]@4",
                "Delete [y]@14");
        }

        #endregion

        #region Pattern

        [Fact]
        public void ConstantPattern_Update()
        {
            var src1 = @"
if ((o is null) && (y == 7)) return 3;
if (a is 7) return 5;
";

            var src2 = @"
if ((o1 is null) && (y == 7)) return 3;
if (a is 77) return 5;
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [if ((o is null) && (y == 7)) return 3;]@4 -> [if ((o1 is null) && (y == 7)) return 3;]@4",
                "Update [if (a is 7) return 5;]@44 -> [if (a is 77) return 5;]@45");
        }

        [Fact]
        public void DeclarationPattern_Update()
        {
            var src1 = @"
if (!(o is int i) && (y == 7)) return;
if (!(a is string s)) return;
if (!(b is string t)) return;
if (!(c is int j)) return;
";

            var src2 = @"
if (!(o1 is int i) && (y == 7)) return;
if (!(a is int s)) return;
if (!(b is string t1)) return;
if (!(c is int)) return;
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [if (!(o is int i) && (y == 7)) return;]@4 -> [if (!(o1 is int i) && (y == 7)) return;]@4",
                "Update [if (!(a is string s)) return;]@44 -> [if (!(a is int s)) return;]@45",
                "Update [if (!(c is int j)) return;]@106 -> [if (!(c is int)) return;]@105",
                "Update [t]@93 -> [t1]@91",
                "Delete [j]@121");
        }

        [Fact]
        public void DeclarationPattern_Reorder()
        {
            var src1 = @"if ((a is int i) && (b is int j)) { A(); }";
            var src2 = @"if ((b is int j) && (a is int i)) { A(); }";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [if ((a is int i) && (b is int j)) { A(); }]@2 -> [if ((b is int j) && (a is int i)) { A(); }]@2",
                "Reorder [j]@32 -> @16");
        }

        [Fact]
        public void CasePattern_UpdateInsert()
        {
            var src1 = @"
switch(shape)
{
    case Circle c: return 1;
    default: return 4;
}
";

            var src2 = @"
switch(shape)
{
    case Circle c1: return 1;
    case Point p: return 0;
    default: return 4;
}
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [case Circle c: return 1;]@26 -> [case Circle c1: return 1;]@26",
                "Insert [case Point p: return 0;]@57",
                "Insert [case Point p:]@57",
                "Insert [return 0;]@71",
                "Update [c]@38 -> [c1]@38",
                "Insert [p]@68");
        }

        [Fact]
        public void CasePattern_UpdateDelete()
        {
            var src1 = @"
switch(shape)
{
    case Point p: return 0;
    case Circle c: A(c); break;
    default: return 4;
}
";

            var src2 = @"
switch(shape)
{
    case Circle c1: A(c1); break;
    default: return 4;
}
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [case Circle c: A(c); break;]@55 -> [case Circle c1: A(c1); break;]@26",
                "Update [A(c);]@70 -> [A(c1);]@42",
                "Update [p]@37 -> [c1]@38",
                "Move [p]@37 -> @38",
                "Delete [case Point p: return 0;]@26",
                "Delete [case Point p:]@26",
                "Delete [return 0;]@40",
                "Delete [c]@67");
        }

        [Fact]
        public void WhenCondition_Update()
        {
            var src1 = @"
switch(shape)
{
    case Circle c when (c < 10): return 1;
    case Circle c when (c > 100): return 2;
}
";

            var src2 = @"
switch(shape)
{
    case Circle c when (c < 5): return 1;
    case Circle c2 when (c2 > 100): return 2;
}
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [case Circle c when (c < 10): return 1;]@26 -> [case Circle c when (c < 5): return 1;]@26",
                "Update [case Circle c when (c > 100): return 2;]@70 -> [case Circle c2 when (c2 > 100): return 2;]@69",
                "Update [when (c < 10)]@40 -> [when (c < 5)]@40",
                "Update [c]@82 -> [c2]@81",
                "Update [when (c > 100)]@84 -> [when (c2 > 100)]@84");
        }

        [Fact]
        public void CasePatternWithWhenCondition_UpdateReorder()
        {
            var src1 = @"
switch(shape)
{
    case Rectangle r: return 0;
    case Circle c when (c.Radius < 10): return 1;
    case Circle c when (c.Radius > 100): return 2;
}
";

            var src2 = @"
switch(shape)
{
    case Circle c when (c.Radius > 99): return 2;
    case Circle c when (c.Radius < 10): return 1;
    case Rectangle r: return 0;
}
";
            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [case Circle c when (c.Radius < 10): return 1;]@59 -> @77",
                "Reorder [case Circle c when (c.Radius > 100): return 2;]@110 -> @26",
                "Update [case Circle c when (c.Radius > 100): return 2;]@110 -> [case Circle c when (c.Radius > 99): return 2;]@26",
                "Move [c]@71 -> @38",
                "Update [when (c.Radius > 100)]@124 -> [when (c.Radius > 99)]@40",
                "Move [c]@122 -> @89");
        }

        #endregion

        #region Ref

        [Fact]
        public void Ref_Update()
        {
            var src1 = @"
ref int a = ref G(new int[] { 1, 2 });
ref int G(int[] p) { return ref p[1];  }
";

            var src2 = @"
ref int32 a = ref G1(new int[] { 1, 2 });
ref int G1(int[] p) { return ref p[2]; }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [ref int G(int[] p) { return ref p[1];  }]@44 -> [ref int G1(int[] p) { return ref p[2]; }]@47",
                "Update [ref int a = ref G(new int[] { 1, 2 })]@4 -> [ref int32 a = ref G1(new int[] { 1, 2 })]@4",
                "Update [a = ref G(new int[] { 1, 2 })]@12 -> [a = ref G1(new int[] { 1, 2 })]@14");
        }

        [Fact]
        public void Ref_Insert()
        {
            var src1 = @"
int a = G(new int[] { 1, 2 });
int G(int[] p) { return p[1];  }
";

            var src2 = @"
ref int32 a = ref G1(new int[] { 1, 2 });
ref int G1(int[] p) { return ref p[2]; }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [int G(int[] p) { return p[1];  }]@36 -> [ref int G1(int[] p) { return ref p[2]; }]@47",
                "Update [int a = G(new int[] { 1, 2 })]@4 -> [ref int32 a = ref G1(new int[] { 1, 2 })]@4",
                "Update [a = G(new int[] { 1, 2 })]@8 -> [a = ref G1(new int[] { 1, 2 })]@14");
        }

        [Fact]
        public void Ref_Delete()
        {
            var src1 = @"
ref int a = ref G(new int[] { 1, 2 });
ref int G(int[] p) { return ref p[1];  }
";

            var src2 = @"
int32 a = G1(new int[] { 1, 2 });
int G1(int[] p) { return p[2]; }
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Update [ref int G(int[] p) { return ref p[1];  }]@44 -> [int G1(int[] p) { return p[2]; }]@39",
                "Update [ref int a = ref G(new int[] { 1, 2 })]@4 -> [int32 a = G1(new int[] { 1, 2 })]@4",
                "Update [a = ref G(new int[] { 1, 2 })]@12 -> [a = G1(new int[] { 1, 2 })]@10");
        }

        #endregion

        #region Tuples

        [Fact]
        public void TupleType_LocalVariables()
        {
            var src1 = @"
(int a, string c) x = (a, string2);
(int a, int b) y = (3, 4);
(int a, int b, int c) z = (5, 6, 7);
";

            var src2 = @"
(int a, int b)  x = (a, string2);
(int a, int b, string c) z1 = (5, 6, 7);
(int a, int b) y2 = (3, 4);
";

            var edits = GetMethodEdits(src1, src2);

            edits.VerifyEdits(
                "Reorder [(int a, int b, int c) z = (5, 6, 7);]@69 -> @39",
                "Update [(int a, string c) x = (a, string2)]@4 -> [(int a, int b)  x = (a, string2)]@4",
                "Update [(int a, int b, int c) z = (5, 6, 7)]@69 -> [(int a, int b, string c) z1 = (5, 6, 7)]@39",
                "Update [z = (5, 6, 7)]@91 -> [z1 = (5, 6, 7)]@64",
                "Update [y = (3, 4)]@56 -> [y2 = (3, 4)]@96");
        }

        [Fact]
        public void TupleElementName()
        {
            var src1 = @"(int a, int b) F();";
            var src2 = @"(int x, int b) F();";

            var edits = GetTopEdits(src1, src2);

            edits.VerifyEdits(
                "Update [(int a, int b) F();]@0 -> [(int x, int b) F();]@0");
        }

        [Fact]
        public void TupleInField()
        {
            var src1 = @"private (int, int) _x = (1, 2);";
            var src2 = @"private (int, string) _y = (1, 2);";

            var edits = GetTopEdits(src1, src2);

            edits.VerifyEdits(
                "Update [(int, int) _x = (1, 2)]@8 -> [(int, string) _y = (1, 2)]@8",
                "Update [_x = (1, 2)]@19 -> [_y = (1, 2)]@22");
        }

        [Fact]
        public void TupleInProperty()
        {
            var src1 = @"public (int, int) Property1 { get { return (1, 2); } }";
            var src2 = @"public (int, string) Property2 { get { return (1, string.Empty); } }";

            var edits = GetTopEdits(src1, src2);

            edits.VerifyEdits(
                "Update [public (int, int) Property1 { get { return (1, 2); } }]@0 -> [public (int, string) Property2 { get { return (1, string.Empty); } }]@0",
                "Update [get { return (1, 2); }]@30 -> [get { return (1, string.Empty); }]@33");
        }

        [Fact]
        public void TupleInDelegate()
        {
            var src1 = @"public delegate void EventHandler1((int, int) x);";
            var src2 = @"public delegate void EventHandler2((int, int) y);";

            var edits = GetTopEdits(src1, src2);

            edits.VerifyEdits(
                "Update [public delegate void EventHandler1((int, int) x);]@0 -> [public delegate void EventHandler2((int, int) y);]@0",
                "Update [(int, int) x]@35 -> [(int, int) y]@35");
        }

        #endregion 
    }
}