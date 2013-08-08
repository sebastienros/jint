/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.3/11.3.1/11.3.1-2-1-s.js
 * @description Strict Mode - SyntaxError is thrown if the identifier 'arguments' appear as a PostfixExpression(arguments++)
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var blah = arguments;
        try {
            eval("arguments++;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && blah === arguments;
        }
    }
runTestCase(testcase);
