/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-602.js
 * @description ES5 Attributes - all attributes in Object.defineProperty are correct
 */


function testcase() {
        var desc = Object.getOwnPropertyDescriptor(Object, "defineProperty");

        var propertyAreCorrect = (desc.writable === true && desc.enumerable === false && desc.configurable === true);
        var temp = Object.defineProperty;
        try {
            Object.defineProperty = "2010";

            var isWritable = (Object.defineProperty === "2010");

            var isEnumerable = false;

            for (var prop in Object) {
                if (prop === "defineProperty") {
                    isEnumerable = true;
                }
            }

            delete Object.defineProperty;

            var isConfigurable = !Object.hasOwnProperty("defineProperty");

            return propertyAreCorrect && isWritable && !isEnumerable && isConfigurable;
        } finally {
            Object.defineProperty = temp;
            Object.defineProperty(Object, "defineProperty", {
                enumerable: false
            });
        }
    }
runTestCase(testcase);
