/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.5/15.5.4/15.5.4.20/15.5.4.20-2-45.js
 * @description String.prototype.trim - 'this' is a string that contains white space, character, number, object and null characters
 */


function testcase() {
        var str = "abc" + "   " + 123 + "   " + {} + "    " + "\u0000";
        var str1 = "    " + str + "    ";
        return str1.trim() === str;
    }
runTestCase(testcase);
