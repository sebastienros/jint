/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.6/15.2.3.6-4-293-1.js
 * @description Object.defineProperty - 'O' is an Arguments object, 'name' is own data property of 'O', test TypeError is not thrown when updating the [[Value]] attribute value of 'name' which is defined as non-writable and configurable (10.6 [[DefineOwnProperty]] step 3 and 5b)
 */


function testcase() {
        return (function () {
            Object.defineProperty(arguments, "0", {
                value: 10,
                writable: false
            });
            Object.defineProperty(arguments, "0", {
                value: 20
            });
            return dataPropertyAttributesAreCorrect(arguments, "0", 20, false, true, true);
        }(0, 1, 2));
    }
runTestCase(testcase);
