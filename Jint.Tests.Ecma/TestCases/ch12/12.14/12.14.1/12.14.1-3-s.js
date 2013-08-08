/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.14/12.14.1/12.14.1-3-s.js
 * @description Strict Mode - SyntaxError isn't thrown if a TryStatement with a Catch occurs within strict code and the Identifier of the Catch production is EVAL but throws SyntaxError if it is eval
 * @onlyStrict
 */


function testcase() {
        "use strict";

       try{ eval(" try { \
             throw new Error(\"...\");\
             return false;\
         } catch (EVAL) {\
             try\
             {\
               throw new Error(\"...\");\
             }catch(eval)\
             {\
                 return EVAL instanceof Error;\
              }\
         }");
         return false;
        } catch(e) {
             return e instanceof SyntaxError;
        }
    }
runTestCase(testcase);
