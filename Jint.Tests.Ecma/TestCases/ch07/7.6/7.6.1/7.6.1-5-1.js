/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-1.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: null, true, false
 */


function testcase() {
        var tokenCodes  = { 
            null: 0,
            true: 1,
            false: 2
        };
        var arr = [
            'null',
            'true',
            'false'
        ];  
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
