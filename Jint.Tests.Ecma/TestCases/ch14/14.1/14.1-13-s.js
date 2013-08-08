/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-13-s.js
 * @description semicolon insertion works for'use strict' directive
 * @noStrict
 */


function testcase() {

  function foo()
  {
    "use strict"
     return (this === undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
