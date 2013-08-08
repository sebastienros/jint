/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-516.js
 * @description ES5 Attributes - success to update [[Get]] attribute of accessor property ([[Get]] is a Function, [[Set]] is undefined, [[Enumerable]] is false, [[Configurable]] is true) to different value
 */


function testcase() {
        var obj = {};
        var getFunc = function () {
            return 1001;
        };

        Object.defineProperty(obj, "prop", {
            get: getFunc,
            set: undefined,
            enumerable: false,
            configurable: true
        });

        var result1 = obj.prop === 1001;
        var desc1 = Object.getOwnPropertyDescriptor(obj, "prop");

        Object.defineProperty(obj, "prop", {
            get: undefined
        });

        var result2 = typeof obj.prop === "undefined";
        var desc2 = Object.getOwnPropertyDescriptor(obj, "prop");

        return result1 && result2 && desc1.get === getFunc && typeof desc2.get === "undefined";
    }
runTestCase(testcase);
