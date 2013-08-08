/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-6-4.js
 * @description Allow reserved words as property names by dot operator assignment, accessed via indexing: new, var, catch
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes.new = 0;
        tokenCodes.var = 1;
        tokenCodes.catch = 2;
        var arr = [
            'new', 
            'var', 
            'catch'
         ];
         for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
