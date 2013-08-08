/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * Refer 12.6.3; 
 * The production 
 *     IterationStatement : for ( var VariableDeclarationListNoIn ; Expressionopt ; Expressionopt ) Statement
 * is evaluated as follows:
 *
 * @path ch12/12.6/12.6.3/12.6.3_2-3-a-ii-6.js
 * @description The for Statement - (normal, V, empty) will be returned when first Expression is a Number object (value is a positive)
 */


function testcase() {
        var accessed = false;
        var numObj = new Number(12);
        for (var i = 0; numObj;) {
            accessed = true;
            break;
        }
        return accessed;
    }
runTestCase(testcase);
