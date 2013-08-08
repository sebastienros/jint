/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-313.js
 * @description Object.defineProperty - 'O' is an Arguments object, 'P' is generic property, and 'desc' is data descriptor, test 'P' is defined in 'O' with all correct attribute values (10.6 [[DefineOwnProperty]] step 3)
 */


function testcase() {
        return (function () {
            Object.defineProperty(arguments, "genericProperty", {
                value: 1001,
                writable: true,
                enumerable: true,
                configurable: true
            });
            return dataPropertyAttributesAreCorrect(arguments, "genericProperty", 1001, true, true, true);
        }(1, 2, 3));
    }
runTestCase(testcase);
