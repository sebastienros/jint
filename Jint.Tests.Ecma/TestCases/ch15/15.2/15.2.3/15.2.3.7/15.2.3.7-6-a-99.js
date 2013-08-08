/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-99.js
 * @description Object.defineProperties - 'P' is data property, P.configurable is true and properties.configurable is false
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: 200, 
            enumerable: true, 
            writable: true, 
            configurable: true 
        });

        Object.defineProperties(obj, {
            foo: {
                configurable: false
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", 200, true, true, false);
    }
runTestCase(testcase);
