/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-15.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: package, protected, static
 */


function testcase() {
        var tokenCodes = {
            package: 0,
            protected: 1,
            static: 2
        };
        var arr = [
            'package',
            'protected',
            'static'
        ];  
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
