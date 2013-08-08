/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-4-s.js
 * @description StrictMode - A TypeError is thrown when a code in strict mode tries to write to 'arguments' of function instances.
 * @onlyStrict
 */


function testcase() {
        "use strict";
        try {
            var foo = function () {
            }
            foo.arguments = 20;
            return false;
        } catch (ex) {
            return ex instanceof TypeError;
        }
    }
runTestCase(testcase);
