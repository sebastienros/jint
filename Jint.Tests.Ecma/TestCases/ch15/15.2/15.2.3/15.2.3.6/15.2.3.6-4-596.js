/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-596.js
 * @description ES5 Attributes - Fail to update value of property into of [[Proptotype]] internal property (Function.prototype.bind)
 */


function testcase() {
        var foo = function () { };
        var data = "data";
        try {
            Object.defineProperty(Function.prototype, "prop", {
                get: function () {
                    return data;
                },
                enumerable: false,
                configurable: true
            });

            var obj = foo.bind({});
            obj.prop = "overrideData";

            return !obj.hasOwnProperty("prop") && obj.prop === "data";
        } finally {
            delete Function.prototype.prop;
        }
    }
runTestCase(testcase);
