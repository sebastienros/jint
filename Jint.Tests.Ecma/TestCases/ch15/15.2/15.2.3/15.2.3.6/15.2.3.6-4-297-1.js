/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-297-1.js
 * @description Object.defineProperty - 'O' is an Arguments object of a function that has formal parameters, 'name' is own accessor property of 'O' which is also defined in [[ParameterMap]] of 'O', test TypeError is thrown when updating the [[Get]] attribute value of 'name' which is defined as non-configurable (10.6 [[DefineOwnProperty]] step 4 and step 5a)
 */


function testcase() {
        return (function (a, b, c) {
            function getFunc1() {
                return 10;
            }
            Object.defineProperty(arguments, "0", {
                get: getFunc1,
                enumerable: false,
                configurable: false
            });
            function getFunc2() {
                return 20;
            }
            try {
                Object.defineProperty(arguments, "0", {
                    get: getFunc2
                });
            } catch (e) {
                var verifyFormal = a === 0;
                return e instanceof TypeError && accessorPropertyAttributesAreCorrect(arguments, "0", getFunc1, undefined, undefined, false, false) && verifyFormal;
            }
            return false;
        }(0, 1, 2));
    }
runTestCase(testcase);
