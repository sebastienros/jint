/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-10.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: in, try, class
 */


function testcase() {
        var tokenCodes  = { 
            in: 0, 
            try: 1,
            class: 2
        };
        var arr = [
            'in', 
            'try',
            'class'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
