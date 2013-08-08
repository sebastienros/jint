/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-6.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: continue, for, switch
 */


function testcase() {
        var tokenCodes  = { 
            continue: 0, 
            for: 1, 
            switch: 2
        };
        var arr = [
            'continue', 
            'for',
            'switch'
        ];  
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
