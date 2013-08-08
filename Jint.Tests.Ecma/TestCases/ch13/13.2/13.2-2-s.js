/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-2-s.js
 * @description StrictMode - A TypeError is thrown when a strict mode code writes to properties named 'caller' of function instances.
 * @onlyStrict
 */


function testcase() {
        "use strict";
        try {
            var foo = function () {
            }
            foo.caller = 20;
            return false;
        } catch (ex) {
            return ex instanceof TypeError;
        }
    }
runTestCase(testcase);
