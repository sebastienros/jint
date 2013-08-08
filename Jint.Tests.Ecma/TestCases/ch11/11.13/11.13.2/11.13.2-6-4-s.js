/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.13/11.13.2/11.13.2-6-4-s.js
 * @description Strict Mode - SyntaxError is thrown if the identifier eval appear as the LeftHandSideExpression of a Compound Assignment operator(+=)
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var blah = eval;
        try {
            eval("eval += 20;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && blah === eval;
        }
    }
runTestCase(testcase);
