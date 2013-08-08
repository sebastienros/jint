/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-420.js
 * @description ES5 Attributes - Failed to add a property to an object when the object's prototype has a property with the same name and [[Writable]] set to false(Function.prototype.bind)
 */


function testcase() {
        var foo = function () { };
        try {
            Object.defineProperty(Function.prototype, "prop", {
                value: 1001,
                writable: false,
                enumerable: false,
                configurable: true
            });

            var obj = foo.bind({});
            obj.prop = 1002;

            return !obj.hasOwnProperty("prop") && obj.prop === 1001;
        } finally {
            delete Function.prototype.prop;
        }
    }
runTestCase(testcase);
