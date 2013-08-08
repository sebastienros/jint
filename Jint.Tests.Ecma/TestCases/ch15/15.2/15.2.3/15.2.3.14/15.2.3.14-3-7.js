/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-7.js
 * @description Object.keys - length of the returned array equals the number of own enumerable properties of 'O'
 */


function testcase() {
        var obj = { prop1: 1001, prop2: 1002 };

        Object.defineProperty(obj, "prop3", {
            value: 1003,
            enumerable: true,
            configurable: true
        });

        Object.defineProperty(obj, "prop4", {
            get: function () {
                return 1003;
            },
            enumerable: false,
            configurable: true
        });

        var arr = Object.keys(obj);

        return arr.length === 3;
    }
runTestCase(testcase);
