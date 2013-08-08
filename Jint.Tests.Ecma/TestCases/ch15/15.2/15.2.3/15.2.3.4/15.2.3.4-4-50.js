/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-4-50.js
 * @description Object.getOwnPropertyNames - non-enumerable own property of 'O' is pushed into the returned Array
 */


function testcase() {
        var obj = {};

        Object.defineProperty(obj, "nonEnumerableProp", {
            value: 10,
            enumerable: false,
            configurable: true
        });

        var result = Object.getOwnPropertyNames(obj);

        return result[0] === "nonEnumerableProp";
    }
runTestCase(testcase);
