/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-9.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: if, throw, delete
 */


function testcase() {
        var tokenCodes  = { 
            if: 0, 
            throw: 1, 
            delete: 2
        };
        var arr = [
            'if', 
            'throw', 
            'delete'
        ];   
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
