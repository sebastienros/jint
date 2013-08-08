/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-1.js
 * @description Object.getOwnPropertyNames returns array of property names (Global)
 */


function testcase() {
        var result = Object.getOwnPropertyNames(fnGlobalObject());
        var expResult = ["NaN", "Infinity", "undefined", "eval", "parseInt", "parseFloat", "isNaN", "isFinite", "decodeURI", "decodeURIComponent", "encodeURI", "encodeURIComponent", "Object", "Function", "Array", "String", "Boolean", "Number", "Date", "Date", "RegExp", "Error", "EvalError", "RangeError", "ReferenceError", "SyntaxError", "TypeError", "URIError", "Math", "JSON"];

        var result1 = {};
        for (var p in result) {
            result1[result[p]] = true;
        }

        for (var p1 in expResult) {
            if (!result1[expResult[p1]]) {
                return false;
            }
        }

        return true;
    }
runTestCase(testcase);
