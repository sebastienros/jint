/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-12-s.js
 * @description comments may follow 'use strict' directive
 * @noStrict
 */


function testcase() {

  function foo()
  {
     "use strict";    /* comment */   // comment

     return (this === undefined);
  }

  return foo.call(undefined);
 }
runTestCase(testcase);
