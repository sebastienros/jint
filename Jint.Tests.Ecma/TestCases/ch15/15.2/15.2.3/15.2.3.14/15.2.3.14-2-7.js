/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-2-7.js
 * @description Object.keys - 'n' is 0 when 'O' doesn't contain own enumerable data or accessor properties
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "prop1", {
            value: 1001,
            enumerable: false,
            configurable: true
        });

        Object.defineProperty(obj, "prop2", {
            get: function () {
                return 1002;
            },
            enumerable: false,
            configurable: true
        });

        var arr = Object.keys(obj);

        return arr.length === 0;
    }
runTestCase(testcase);
