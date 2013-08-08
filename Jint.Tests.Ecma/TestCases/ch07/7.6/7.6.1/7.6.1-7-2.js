/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-7-2.js
 * @description Allow reserved words as property names by index assignment, accessed via indexing: break, case, do
 */


function testcase() {
        var tokenCodes = {};
        tokenCodes['break'] = 0;
        tokenCodes['case'] = 1;
        tokenCodes['do'] = 2;     
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
