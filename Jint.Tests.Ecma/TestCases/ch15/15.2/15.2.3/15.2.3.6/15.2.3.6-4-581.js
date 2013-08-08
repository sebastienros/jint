/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-581.js
 * @description ES5 Attributes - Fail to add property into object (Number instance)
 */


function testcase() {
        var data = "data";
        try {
            Object.defineProperty(Number.prototype, "prop", {
                get: function () {
                    return data;
                },
                enumerable: false,
                configurable: true
            });
            var numObj = new Number();
            numObj.prop = "myOwnProperty";

            return !numObj.hasOwnProperty("prop") && numObj.prop === "data" && data === "data";
        } finally {
            delete Number.prototype.prop;
        }
    }
runTestCase(testcase);
