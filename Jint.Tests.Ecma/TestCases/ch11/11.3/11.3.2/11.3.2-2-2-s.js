/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.3/11.3.2/11.3.2-2-2-s.js
 * @description Strict Mode - SyntaxError is thrown if the identifier 'eval' appear as a PostfixExpression(eval--)
 * @onlyStrict
 */


function testcase() {
        "use strict";
        var blah = eval;
        try {
            eval("eval--;");
            return false;
        } catch (e) {
            return e instanceof SyntaxError && blah === eval;
        }
    }
runTestCase(testcase);
