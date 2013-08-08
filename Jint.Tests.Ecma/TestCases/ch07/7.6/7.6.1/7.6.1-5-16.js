/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-16.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: undefined, NaN, Infinity
 */


function testcase() {
        var tokenCodes = {
            undefined: 0,
            NaN: 1,
            Infinity: 2
        };
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
