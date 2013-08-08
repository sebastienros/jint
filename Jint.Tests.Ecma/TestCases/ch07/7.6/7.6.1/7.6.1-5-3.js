/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-3.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: instanceof, typeof, else
 */


function testcase() {
        var tokenCodes  = { 
            instanceof: 0,
            typeof: 1,
            else: 2
        };
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
