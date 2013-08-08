/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.2/11.13.2-6-12-s.js
 * @description Strict Mode - SyntaxError is thrown if the identifier arguments appear as the LeftHandSideExpression of a Compound Assignment operator(*=)
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var blah = arguments;
        try {
            eval("arguments *= 20;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && blah === arguments;
        }
    }
runTestCase(testcase);
