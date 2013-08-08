/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-304.js
 * @description Object.defineProperties - 'O' is an Arguments object, 'P' is generic own accessor property of 'O', and 'desc' is accessor descriptor, test updating multiple attribute values of 'P' (10.6 [[DefineOwnProperty]] step 4)
 */


function testcase() {
        var arg = (function () {
            return arguments;
        }(1, 2, 3));

        Object.defineProperty(arg, "genericProperty", {
            get: function () {
                return 1001;
            },
            set: function (value) {
                arg.testGetFunction1 = value;
            },
            enumerable: true,
            configurable: true
        });

        function getFun() {
            return "getFunctionString";
        }
        function setFun(value) {
            arg.testGetFunction = value;
        }
        Object.defineProperties(arg, {
            "genericProperty": {
                get: getFun,
                set: setFun,
                enumerable: false,
                configurable: false
            }
        });

        return accessorPropertyAttributesAreCorrect(arg, "genericProperty", getFun, setFun, "testGetFunction", false, false);
    }
runTestCase(testcase);
