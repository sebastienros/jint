/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-305.js
 * @description Object.defineProperties - 'O' is an Arguments object, 'P' is generic own data property of 'O', and 'desc' is data descriptor, test updating multiple attribute values of 'P' (10.6 [[DefineOwnProperty]] step 4)
 */


function testcase() {
        var arg = (function () {
            return arguments;
        }(1, 2, 3));

        Object.defineProperty(arg, "genericProperty", {
            value: 1001,
            writable: true,
            enumerable: true,
            configurable: true
        });

        Object.defineProperties(arg, {
            "genericProperty": {
                value: 1002,
                enumerable: false,
                configurable: false
            }
        });

        return dataPropertyAttributesAreCorrect(arg, "genericProperty", 1002, true, false, false);
    }
runTestCase(testcase);
