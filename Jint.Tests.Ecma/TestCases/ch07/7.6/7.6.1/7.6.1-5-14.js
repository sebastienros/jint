/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-14.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: public, yield, interface
 */


function testcase() {
        var tokenCodes = {
            public: 0,
            yield: 1,
            interface: 2
        };
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
