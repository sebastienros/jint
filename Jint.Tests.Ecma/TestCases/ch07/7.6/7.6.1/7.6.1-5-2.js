/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-2.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: break, case, do
 */


function testcase() {
        var tokenCodes  = { 
            break: 0,
            case: 1,
            do: 2
        };
        var arr = [
            'break',
            'case',
            'do'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
