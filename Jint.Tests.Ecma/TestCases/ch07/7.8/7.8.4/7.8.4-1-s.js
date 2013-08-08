/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.8/7.8.4/7.8.4-1-s.js
 * @description A directive preceeding an 'use strict' directive may not contain an OctalEscapeSequence
 * @onlyStrict
 */


function testcase()
{
  try 
  {
    eval(' "asterisk: \\052" /* octal escape sequences forbidden in strict mode*/ ; "use strict";');
    return false;
  }
  catch (e) {
    return (e instanceof SyntaxError);
  }
 }
runTestCase(testcase);
