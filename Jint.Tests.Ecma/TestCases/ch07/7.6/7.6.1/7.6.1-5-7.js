/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-7.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: while, debugger, function
 */


function testcase() {
        var tokenCodes  = { 
            while: 0, 
            debugger: 1, 
            function: 2
        };
        var arr = [ 
            'while' ,
            'debugger', 
            'function'
        ];    
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
