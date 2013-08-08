/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-7-13.js
 * @description Allow reserved words as property names by index assignment, accessed via indexing: implements, let, private
 */


function testcase() {
        var tokenCodes = {};
        tokenCodes['implements'] = 0;
        tokenCodes['let'] = 1;
        tokenCodes['private'] = 2;     
        var arr = [
            'implements',
            'let',
            'private'
        ];
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
