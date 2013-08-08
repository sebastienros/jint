/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-7-9.js
 * @description Allow reserved words as property names by index assignment, accessed via indexing: if, throw, delete
 */


function testcase() {
        var tokenCodes = {};
        tokenCodes['if'] = 0;
        tokenCodes['throw'] = 1;
        tokenCodes['delete'] = 2;      
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
