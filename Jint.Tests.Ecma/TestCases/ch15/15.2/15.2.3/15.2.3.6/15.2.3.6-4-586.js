/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-586.js
 * @description ES5 Attributes - Fail to update value of property into of [[Proptotype]] internal property  (JSON)
 */


function testcase() {
        var data = "data";
        try {
            Object.defineProperty(Object.prototype, "prop", {
                get: function () {
                    return data;
                },
                enumerable: false,
                configurable: true
            });
            JSON.prop = "myOwnProperty";

            return !JSON.hasOwnProperty("prop") && JSON.prop === "data" && data === "data";
        } finally {
            delete Object.prototype.prop;
        }
    }
runTestCase(testcase);
