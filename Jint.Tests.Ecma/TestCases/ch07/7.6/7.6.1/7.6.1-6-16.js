/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-6-16.js
 * @description Allow reserved words as property names by dot operator assignment, accessed via indexing: undefined, NaN, Infinity
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes.undefined = 0;
        tokenCodes.NaN = 1;
        tokenCodes.Infinity = 2;
        var arr = [
            'undefined',
            'NaN',
            'Infinity'
         ];
         for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
