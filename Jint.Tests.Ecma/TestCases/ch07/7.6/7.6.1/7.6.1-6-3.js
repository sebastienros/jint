/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-6-3.js
 * @description Allow reserved words as property names by dot operator assignment, accessed via indexing: instanceof, typeof, else
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes.instanceof = 0;
        tokenCodes.typeof = 1;
        tokenCodes.else = 2;
        var arr = [
            'instanceof',
            'typeof',
            'else'
         ];
         for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
