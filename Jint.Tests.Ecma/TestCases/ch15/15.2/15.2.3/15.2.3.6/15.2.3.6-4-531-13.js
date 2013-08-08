/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-531-13.js
 * @description Object.defineProperty will update [[Get]] and [[Set]] attributes of indexed accessor property 'P' successfully when [[Configurable]] attribute is true, 'O' is the global object (8.12.9 step 11)
 */


function testcase() {

        var obj = fnGlobalObject();
        try {
            obj.verifySetFunction = "data";
            Object.defineProperty(obj, "0", {
                get: function () {
                    return obj.verifySetFunction;
                },
                set: function (value) {
                    obj.verifySetFunction = value;
                },
                configurable: true
            });

            obj.verifySetFunction1 = "data1";
            var getFunc = function () {
                return obj.verifySetFunction1;
            };
            var setFunc = function (value) {
                obj.verifySetFunction1 = value;
            };

            Object.defineProperty(obj, "0", {
                get: getFunc,
                set: setFunc
            });

            return accessorPropertyAttributesAreCorrect(obj, "0", getFunc, setFunc, "verifySetFunction1", false, true);
        } finally {
            delete obj[0];
            delete obj.verifySetFunction;
            delete obj.verifySetFunction1;
        }
    }
runTestCase(testcase);
