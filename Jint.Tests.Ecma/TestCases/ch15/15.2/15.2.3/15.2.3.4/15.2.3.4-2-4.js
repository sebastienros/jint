/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.4/15.2.3.4-2-4.js
 * @description Object.getOwnPropertyNames - returned array is the standard built-in constructor
 */


function testcase() {
        var oldArray = Array;
        Array = function () {
            throw new Error("invoke customer defined Array!");
        };

        var obj = {};
        try {
            var result = Object.getOwnPropertyNames(obj);
            return Object.prototype.toString.call(result) === "[object Array]";
        } catch (ex) {
            return false;
        } finally {
            Array = oldArray;
        }
    }
runTestCase(testcase);
