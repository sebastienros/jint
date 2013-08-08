/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-6-14.js
 * @description Allow reserved words as property names by dot operator assignment, accessed via indexing: public, yield, interface
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes.public = 0;
        tokenCodes.yield = 1;
        tokenCodes.interface = 2;
        var arr = [
            'public',
            'yield',
            'interface'
         ];
         for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
