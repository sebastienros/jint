/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-a-19.js
 * @description Array.prototype.lastIndexOf -  decreasing length of array does not delete non-configurable properties
 */


function testcase() {

        var arr = [0, 1, 2, 3];

        Object.defineProperty(arr, "2", {
            get: function () {
                return "unconfigurable";
            },
            configurable: false
        });

        Object.defineProperty(arr, "3", {
            get: function () {
                arr.length = 2;
                return 1;
            },
            configurable: true
        });

        return 2 === arr.lastIndexOf("unconfigurable");
    }
runTestCase(testcase);
