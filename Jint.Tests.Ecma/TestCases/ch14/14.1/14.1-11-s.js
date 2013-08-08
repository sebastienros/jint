/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch14/14.1/14.1-11-s.js
 * @description comments may preceed 'use strict' directive
 * @noStrict
 */


function testcase() {

  function foo()
  {
     // comment
     /* comment */ "use strict";

   return(this === undefined);

  }

  return foo.call(undefined);
 }
runTestCase(testcase);
